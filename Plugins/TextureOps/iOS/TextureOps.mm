#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <MobileCoreServices/UTCoreTypes.h>
#import <ImageIO/ImageIO.h>

#ifdef UNITY_4_0 || UNITY_5_0
#import "iPhone_View.h"
#else
extern UIViewController* UnityGetGLViewController();
#endif

@interface UTextureOps:NSObject
+ (char *)getImageProperties:(NSString *)path;
+ (char *)getVideoProperties:(NSString *)path;
+ (char *)loadImageAtPath:(NSString *)path tempFilePath:(NSString *)tempFilePath maximumSize:(int)maximumSize;
@end

@implementation UTextureOps

// Credit: https://stackoverflow.com/a/4170099/2373034
+ (NSArray *)getImageMetadata:(NSString *)path
{
	int width = 0;
	int height = 0;
	int orientation = -1;

	CGImageSourceRef imageSource = CGImageSourceCreateWithURL( (__bridge CFURLRef) [NSURL fileURLWithPath:path], nil );
	if( imageSource != nil )
	{
		NSDictionary *options = [NSDictionary dictionaryWithObject:[NSNumber numberWithBool:NO] forKey:(__bridge NSString *)kCGImageSourceShouldCache];
		CFDictionaryRef imageProperties = CGImageSourceCopyPropertiesAtIndex( imageSource, 0, (__bridge CFDictionaryRef) options );
		CFRelease( imageSource );

		CGFloat widthF = 0.0f, heightF = 0.0f;
		if( imageProperties != nil )
		{
			if( CFDictionaryContainsKey( imageProperties, kCGImagePropertyPixelWidth ) )
				CFNumberGetValue( (CFNumberRef) CFDictionaryGetValue( imageProperties, kCGImagePropertyPixelWidth ), kCFNumberCGFloatType, &widthF );
			
			if( CFDictionaryContainsKey( imageProperties, kCGImagePropertyPixelHeight ) )
				CFNumberGetValue( (CFNumberRef) CFDictionaryGetValue( imageProperties, kCGImagePropertyPixelHeight ), kCFNumberCGFloatType, &heightF );

			if( CFDictionaryContainsKey( imageProperties, kCGImagePropertyOrientation ) )
			{
				CFNumberGetValue( (CFNumberRef) CFDictionaryGetValue( imageProperties, kCGImagePropertyOrientation ), kCFNumberIntType, &orientation );
				
				if( orientation > 4 )
				{
					// Landscape image
					CGFloat temp = widthF;
					widthF = heightF;
					heightF = temp;
				}
			}

			CFRelease( imageProperties );
		}

		width = (int) roundf( widthF );
		height = (int) roundf( heightF );
	}

	return [[NSArray alloc] initWithObjects:[NSNumber numberWithInt:width], [NSNumber numberWithInt:height], [NSNumber numberWithInt:orientation], nil];
}

+ (char *)getImageProperties:(NSString *)path
{
	NSArray *metadata = [self getImageMetadata:path];
	
	int orientationUnity;
	int orientation = [metadata[2] intValue];
	
	// To understand the magic numbers, see ImageOrientation enum in TextureOps.cs
	// and http://sylvana.net/jpegcrop/exif_orientation.html
	if( orientation == 1 )
		orientationUnity = 0;
	else if( orientation == 2 )
		orientationUnity = 4;
	else if( orientation == 3 )
		orientationUnity = 2;
	else if( orientation == 4 )
		orientationUnity = 6;
	else if( orientation == 5 )
		orientationUnity = 5;
	else if( orientation == 6 )
		orientationUnity = 1;
	else if( orientation == 7 )
		orientationUnity = 7;
	else if( orientation == 8 )
		orientationUnity = 3;
	else
		orientationUnity = -1;
	
	return [self getCString:[NSString stringWithFormat:@"%d>%d> >%d", [metadata[0] intValue], [metadata[1] intValue], orientationUnity]];
}

+ (char *)getVideoProperties:(NSString *)path
{
	CGSize size = CGSizeZero;
	float rotation = 0;
	long long duration = 0;
	
	AVURLAsset *asset = [AVURLAsset URLAssetWithURL:[NSURL fileURLWithPath:path] options:nil];
	if( asset != nil )
	{
		duration = (long long) round( CMTimeGetSeconds( [asset duration] ) * 1000 );
		CGAffineTransform transform = [asset preferredTransform];
		NSArray<AVAssetTrack *>* videoTracks = [asset tracksWithMediaType:AVMediaTypeVideo];
		if( videoTracks != nil && [videoTracks count] > 0 )
		{
			size = [[videoTracks objectAtIndex:0] naturalSize];
			transform = [[videoTracks objectAtIndex:0] preferredTransform];
		}
		
		rotation = atan2( transform.b, transform.a ) * ( 180.0 / M_PI );
	}
	
	return [self getCString:[NSString stringWithFormat:@"%d>%d>%lld>%f", (int) roundf( size.width ), (int) roundf( size.height ), duration, rotation]];
}

+ (UIImage *)scaleImage:(UIImage *)image maxSize:(int)maxSize
{
	CGFloat width = image.size.width;
	CGFloat height = image.size.height;
	
	UIImageOrientation orientation = image.imageOrientation;
	if( width <= maxSize && height <= maxSize && orientation != UIImageOrientationDown &&
		orientation != UIImageOrientationLeft && orientation != UIImageOrientationRight &&
		orientation != UIImageOrientationLeftMirrored && orientation != UIImageOrientationRightMirrored &&
		orientation != UIImageOrientationUpMirrored && orientation != UIImageOrientationDownMirrored )
		return image;
	
	CGFloat scaleX = 1.0f;
	CGFloat scaleY = 1.0f;
	if( width > maxSize )
		scaleX = maxSize / width;
	if( height > maxSize )
		scaleY = maxSize / height;
	
	// Credit: https://github.com/mbcharbonneau/UIImage-Categories/blob/master/UIImage%2BAlpha.m
	CGImageAlphaInfo alpha = CGImageGetAlphaInfo( image.CGImage );
	BOOL hasAlpha = alpha == kCGImageAlphaFirst || alpha == kCGImageAlphaLast || alpha == kCGImageAlphaPremultipliedFirst || alpha == kCGImageAlphaPremultipliedLast;
	
	CGFloat scaleRatio = scaleX < scaleY ? scaleX : scaleY;
	CGRect imageRect = CGRectMake( 0, 0, width * scaleRatio, height * scaleRatio );
	UIGraphicsBeginImageContextWithOptions( imageRect.size, !hasAlpha, image.scale );
	[image drawInRect:imageRect];
	image = UIGraphicsGetImageFromCurrentImageContext();
	UIGraphicsEndImageContext();
	
	return image;
}

+ (char *)loadImageAtPath:(NSString *)path tempFilePath:(NSString *)tempFilePath maximumSize:(int)maximumSize
{
	// Check if the image can be loaded by Unity without requiring a conversion to PNG
	// Credit: https://stackoverflow.com/a/12048937/2373034
	NSString *extension = [path pathExtension];
	BOOL conversionNeeded = [extension caseInsensitiveCompare:@"jpg"] != NSOrderedSame && [extension caseInsensitiveCompare:@"jpeg"] != NSOrderedSame && [extension caseInsensitiveCompare:@"png"] != NSOrderedSame;

	if( !conversionNeeded )
	{
		// Check if the image needs to be processed at all
		NSArray *metadata = [self getImageMetadata:path];
		int orientationInt = [metadata[2] intValue];  // 1: correct orientation, [1,8]: valid orientation range
		if( orientationInt == 1 && [metadata[0] intValue] <= maximumSize && [metadata[1] intValue] <= maximumSize )
			return [self getCString:path];
	}
	
	UIImage *image = [UIImage imageWithContentsOfFile:path];
	if( image == nil )
		return [self getCString:path];
	
	UIImage *scaledImage = [self scaleImage:image maxSize:maximumSize];
	if( conversionNeeded || scaledImage != image )
	{
		if( ![UIImagePNGRepresentation( scaledImage ) writeToFile:tempFilePath atomically:YES] )
		{
			NSLog( @"Error creating scaled image" );
			return [self getCString:path];
		}
		
		return [self getCString:tempFilePath];
	}
	else
		return [self getCString:path];
}

// Credit: https://stackoverflow.com/a/37052118/2373034
+ (char *)getCString:(NSString *)source
{
	if( source == nil )
		source = @"";
	
	const char *sourceUTF8 = [source UTF8String];
	char *result = (char*) malloc( strlen( sourceUTF8 ) + 1 );
	strcpy( result, sourceUTF8 );
	
	return result;
}

@end

extern "C" char* _TextureOps_GetImageProperties( const char* path )
{
	return [UTextureOps getImageProperties:[NSString stringWithUTF8String:path]];
}

extern "C" char* _TextureOps_GetVideoProperties( const char* path )
{
	return [UTextureOps getVideoProperties:[NSString stringWithUTF8String:path]];
}

extern "C" char* _TextureOps_LoadImageAtPath( const char* path, const char* temporaryFilePath, int maxSize )
{
	return [UTextureOps loadImageAtPath:[NSString stringWithUTF8String:path] tempFilePath:[NSString stringWithUTF8String:temporaryFilePath] maximumSize:maxSize];
}
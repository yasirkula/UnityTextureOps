using System;
using System.Globalization;
using System.IO;
using UnityEngine;
#if UNITY_2018_4_OR_NEWER && !TEXTURE_OPS_DISABLE_ASYNC_FUNCTIONS
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine.Networking;
#endif
using Object = UnityEngine.Object;

public static class TextureOps
{
	// EXIF orientation: http://sylvana.net/jpegcrop/exif_orientation.html (indices are reordered)
	public enum ImageOrientation { Unknown = -1, Normal = 0, Rotate90 = 1, Rotate180 = 2, Rotate270 = 3, FlipHorizontal = 4, Transpose = 5, FlipVertical = 6, Transverse = 7 };

	#region Inner Classes/Structs
	public struct Options
	{
		public readonly bool generateMipmaps;
		public readonly bool linearColorSpace;
		public readonly bool markNonReadable;

		public Options( bool generateMipmaps, bool linearColorSpace, bool markNonReadable )
		{
			this.generateMipmaps = generateMipmaps;
			this.linearColorSpace = linearColorSpace;
			this.markNonReadable = markNonReadable;
		}
	}

	public struct ImageProperties
	{
		public readonly int width;
		public readonly int height;
		public readonly string mimeType;
		public readonly ImageOrientation orientation;

		public ImageProperties( int width, int height, string mimeType, ImageOrientation orientation )
		{
			this.width = width;
			this.height = height;
			this.mimeType = mimeType;
			this.orientation = orientation;
		}
	}

	public struct VideoProperties
	{
		public readonly int width;
		public readonly int height;
		public readonly long duration;
		public readonly float rotation;

		public VideoProperties( int width, int height, long duration, float rotation )
		{
			this.width = width;
			this.height = height;
			this.duration = duration;
			this.rotation = rotation;
		}
	}
	#endregion

	#region Platform Specific Elements
#if !UNITY_EDITOR && UNITY_ANDROID
	private static AndroidJavaClass m_ajc = null;
	private static AndroidJavaClass AJC
	{
		get
		{
			if( m_ajc == null )
				m_ajc = new AndroidJavaClass( "com.yasirkula.unity.TextureOps" );

			return m_ajc;
		}
	}

	private static AndroidJavaObject m_context = null;
	private static AndroidJavaObject Context
	{
		get
		{
			if( m_context == null )
			{
				using( AndroidJavaObject unityClass = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
				{
					m_context = unityClass.GetStatic<AndroidJavaObject>( "currentActivity" );
				}
			}

			return m_context;
		}
	}
#elif !UNITY_EDITOR && UNITY_IOS
	[System.Runtime.InteropServices.DllImport( "__Internal" )]
	private static extern string _TextureOps_GetImageProperties( string path );

	[System.Runtime.InteropServices.DllImport( "__Internal" )]
	private static extern string _TextureOps_GetVideoProperties( string path );

	[System.Runtime.InteropServices.DllImport( "__Internal" )]
	private static extern string _TextureOps_LoadImageAtPath( string path, string temporaryFilePath, int maxSize );
#endif

#if !UNITY_EDITOR && ( UNITY_ANDROID || UNITY_IOS )
	private static string m_temporaryImagePath = null;
	private static string TemporaryImagePath
	{
		get
		{
			if( m_temporaryImagePath == null )
			{
				m_temporaryImagePath = Path.Combine( Application.temporaryCachePath, "__tmpImG" );
				Directory.CreateDirectory( Application.temporaryCachePath );
			}

			return m_temporaryImagePath;
		}
	}
#endif
	#endregion

	private static Material sliceMaterial = null;

	#region Save/Load Functions
	public static bool SaveImage( Texture2D sourceTex, string imagePath )
	{
		bool isJpeg = IsJpeg( imagePath );
		byte[] sourceBytes;

		try
		{
			sourceBytes = isJpeg ? sourceTex.EncodeToJPG( 100 ) : sourceTex.EncodeToPNG();
		}
		catch( UnityException )
		{
			sourceBytes = GetTextureBytesFromCopy( sourceTex, isJpeg );
			if( sourceBytes == null )
				return false;
		}
		catch( ArgumentException )
		{
			sourceBytes = GetTextureBytesFromCopy( sourceTex, isJpeg );
			if( sourceBytes == null )
				return false;
		}

		return SaveImage( sourceBytes, imagePath );
	}

	public static bool SaveImage( byte[] sourceBytes, string imagePath )
	{
		if( sourceBytes == null || sourceBytes.Length == 0 )
			throw new ArgumentException( "Parameter 'sourceBytes' is null or empty!" );

		if( string.IsNullOrEmpty( imagePath ) )
			throw new ArgumentException( "Parameter 'imagePath' is null or empty!" );

		try
		{
			Directory.CreateDirectory( Path.GetDirectoryName( imagePath ) );
			File.WriteAllBytes( imagePath, sourceBytes );
		}
		catch( Exception e )
		{
			Debug.LogException( e );
			return false;
		}

		return true;
	}

	public static Texture2D LoadImage( byte[] imageBytes, TextureFormat format = TextureFormat.RGBA32, Options options = new Options() )
	{
		if( imageBytes == null || imageBytes.Length == 0 )
			throw new ArgumentException( "Parameter 'imageBytes' is null or empty!" );

		Texture2D result = new Texture2D( 2, 2, format, options.generateMipmaps, options.linearColorSpace );
		if( !result.LoadImage( imageBytes, options.markNonReadable ) )
		{
			Object.DestroyImmediate( result );
			result = null;
		}

		return result;
	}

	public static Texture2D LoadImage( string imagePath, int maxSize = -1, Options options = new Options() )
	{
		if( string.IsNullOrEmpty( imagePath ) )
			throw new ArgumentException( "Parameter 'imagePath' is null or empty!" );

		if( !File.Exists( imagePath ) )
			throw new FileNotFoundException( "File not found at " + imagePath );

		if( maxSize <= 0 )
			maxSize = SystemInfo.maxTextureSize;

#if !UNITY_EDITOR && UNITY_ANDROID
		string loadPath = AJC.CallStatic<string>( "LoadImageAtPath", Context, imagePath, TemporaryImagePath, maxSize );
#elif !UNITY_EDITOR && UNITY_IOS
		string loadPath = _TextureOps_LoadImageAtPath( imagePath, TemporaryImagePath, maxSize );
#else
		string loadPath = imagePath;
#endif

		TextureFormat format = IsJpeg( imagePath ) ? TextureFormat.RGB24 : TextureFormat.RGBA32;
		Texture2D result = new Texture2D( 2, 2, format, options.generateMipmaps, options.linearColorSpace );

		try
		{
			if( !result.LoadImage( File.ReadAllBytes( loadPath ), options.markNonReadable ) )
			{
				Object.DestroyImmediate( result );
				return null;
			}
#if UNITY_EDITOR || ( !UNITY_ANDROID && !UNITY_IOS )
			else if( result.width > maxSize || result.height > maxSize )
			{
				int width = result.width;
				int height = result.height;

				ScaleSize( ref width, ref height, maxSize, maxSize );

				Texture2D temp = Scale( result, width, height, format );
				Object.DestroyImmediate( result );
				result = temp;
			}
#endif
		}
		catch( Exception e )
		{
			Debug.LogException( e );

			Object.DestroyImmediate( result );
			return null;
		}
		finally
		{
			if( loadPath != imagePath )
			{
				try
				{
					File.Delete( loadPath );
				}
				catch { }
			}
		}

		return result;
	}

#if UNITY_2018_4_OR_NEWER && !TEXTURE_OPS_DISABLE_ASYNC_FUNCTIONS
	public static async Task<Texture2D> LoadImageAsync( string imagePath, int maxSize = -1, Options options = new Options() )
	{
		if( string.IsNullOrEmpty( imagePath ) )
			throw new ArgumentException( "Parameter 'imagePath' is null or empty!" );

		if( !File.Exists( imagePath ) )
			throw new FileNotFoundException( "File not found at " + imagePath );

		if( maxSize <= 0 )
			maxSize = SystemInfo.maxTextureSize;

#if !UNITY_EDITOR && UNITY_ANDROID
		string loadPath = await Task.Run( () =>
		{
			if( AndroidJNI.AttachCurrentThread() != 0 )
			{
				Debug.LogWarning( "Couldn't attach JNI thread, calling native function on the main thread" );
				return null;
			}
			else
			{
				try
				{
					return AJC.CallStatic<string>( "LoadImageAtPath", Context, imagePath, TemporaryImagePath, maxSize );
				}
				finally
				{
					AndroidJNI.DetachCurrentThread();
				}
			}
		} );
		
		if( string.IsNullOrEmpty( loadPath ) )
			loadPath = AJC.CallStatic<string>( "LoadImageAtPath", Context, imagePath, TemporaryImagePath, maxSize );
#elif !UNITY_EDITOR && UNITY_IOS
		string loadPath = await Task.Run( () => _TextureOps_LoadImageAtPath( imagePath, TemporaryImagePath, maxSize ) );
#else
		string loadPath = imagePath;
#endif

		Texture2D result = null;

		if( !options.linearColorSpace )
		{
			using( UnityWebRequest www = UnityWebRequestTexture.GetTexture( "file://" + loadPath, options.markNonReadable && !options.generateMipmaps ) )
			{
				UnityWebRequestAsyncOperation asyncOperation = www.SendWebRequest();
				while( !asyncOperation.isDone )
					await Task.Yield();

#if UNITY_2020_1_OR_NEWER
				if( www.result != UnityWebRequest.Result.Success )
#else
				if( www.isNetworkError || www.isHttpError )
#endif
				{
					Debug.LogWarning( "Couldn't use UnityWebRequest to load image, falling back to LoadImage: " + www.error );
				}
				else
				{
					Texture2D texture = DownloadHandlerTexture.GetContent( www );

					if( !options.generateMipmaps )
						result = texture;
					else
					{
						Texture2D mipmapTexture = null;
						try
						{
							// Generate a Texture with mipmaps enabled
							// Credits: https://forum.unity.com/threads/generate-mipmaps-at-runtime-for-a-texture-loaded-with-unitywebrequest.644842/#post-7571809
							NativeArray<byte> textureData = texture.GetRawTextureData<byte>();

							mipmapTexture = new Texture2D( texture.width, texture.height, texture.format, true );
#if UNITY_2019_3_OR_NEWER
							mipmapTexture.SetPixelData( textureData, 0 );
#else
							NativeArray<byte> mipmapTextureData = mipmapTexture.GetRawTextureData<byte>();
							NativeArray<byte>.Copy( textureData, mipmapTextureData, textureData.Length );
							mipmapTexture.LoadRawTextureData( mipmapTextureData );
#endif
							mipmapTexture.Apply( true, options.markNonReadable );

							result = mipmapTexture;
						}
						catch( Exception e )
						{
							Debug.LogException( e );

							if( mipmapTexture )
								Object.DestroyImmediate( mipmapTexture );
						}
						finally
						{
							Object.DestroyImmediate( texture );
						}
					}
				}
			}
		}

		if( !result ) // Fallback to Texture2D.LoadImage if something goes wrong
		{
			string extension = Path.GetExtension( imagePath ).ToLowerInvariant();
			TextureFormat format = ( extension == ".jpg" || extension == ".jpeg" ) ? TextureFormat.RGB24 : TextureFormat.RGBA32;

			result = new Texture2D( 2, 2, format, options.generateMipmaps, options.linearColorSpace );

			try
			{
				if( !result.LoadImage( File.ReadAllBytes( loadPath ), options.markNonReadable ) )
				{
					Debug.LogWarning( "Couldn't load image at path: " + loadPath );

					Object.DestroyImmediate( result );
					return null;
				}
			}
			catch( Exception e )
			{
				Debug.LogException( e );

				Object.DestroyImmediate( result );
				return null;
			}
			finally
			{
				if( loadPath != imagePath )
				{
					try
					{
						File.Delete( loadPath );
					}
					catch { }
				}
			}
		}

		return result;
	}
#endif
	#endregion

	#region Texture Operations
	public static Texture2D Crop( Texture sourceTex, int leftOffset, int topOffset, int width, int height, TextureFormat format = TextureFormat.RGBA32, Options options = new Options() )
	{
		if( sourceTex == null )
			throw new ArgumentException( "Parameter 'sourceTex' is null!" );

		if( width <= 0 || width > sourceTex.width )
			width = sourceTex.width;
		if( height <= 0 || height > sourceTex.height )
			height = sourceTex.height;

		if( leftOffset <= 0 )
			leftOffset = 0;
		else if( leftOffset + width > sourceTex.width )
			leftOffset = sourceTex.width - width;

		if( topOffset <= 0 )
			topOffset = 0;
		else if( topOffset + height > sourceTex.height )
			topOffset = sourceTex.height - height;

		Texture2D result = null;

		RenderTexture rt = RenderTexture.GetTemporary( sourceTex.width, sourceTex.height );
		RenderTexture activeRT = RenderTexture.active;

		try
		{
			Graphics.Blit( sourceTex, rt );
			RenderTexture.active = rt;

			result = new Texture2D( width, height, format, options.generateMipmaps, options.linearColorSpace );
			result.ReadPixels( new Rect( leftOffset, sourceTex.height - topOffset - height, width, height ), 0, 0, false );
			result.Apply( options.generateMipmaps, options.markNonReadable );
		}
		catch( Exception e )
		{
			Debug.LogException( e );

			Object.Destroy( result );
			result = null;
		}
		finally
		{
			RenderTexture.active = activeRT;
			RenderTexture.ReleaseTemporary( rt );
		}

		return result;
	}

	public static Texture2D Scale( Texture sourceTex, int targetWidth, int targetHeight, TextureFormat format = TextureFormat.RGBA32, Options options = new Options() )
	{
		if( sourceTex == null )
			throw new ArgumentException( "Parameter 'sourceTex' is null!" );

		Texture2D result = null;

		RenderTexture rt = RenderTexture.GetTemporary( targetWidth, targetHeight );
		RenderTexture activeRT = RenderTexture.active;

		try
		{
			Graphics.Blit( sourceTex, rt );
			RenderTexture.active = rt;

			result = new Texture2D( targetWidth, targetHeight, format, options.generateMipmaps, options.linearColorSpace );
			result.ReadPixels( new Rect( 0, 0, targetWidth, targetHeight ), 0, 0, false );
			result.Apply( options.generateMipmaps, options.markNonReadable );
		}
		catch( Exception e )
		{
			Debug.LogException( e );

			Object.Destroy( result );
			result = null;
		}
		finally
		{
			RenderTexture.active = activeRT;
			RenderTexture.ReleaseTemporary( rt );
		}

		return result;
	}

	public static Texture2D ScaleFill( Texture sourceTex, int targetWidth, int targetHeight, Color32 fillColor, TextureFormat format = TextureFormat.RGBA32, Options options = new Options() )
	{
		if( sourceTex == null )
			throw new ArgumentException( "Parameter 'sourceTex' is null!" );

		Texture2D result = null;

		int preferredWidth = sourceTex.width;
		int preferredHeight = sourceTex.height;

		ScaleSize( ref preferredWidth, ref preferredHeight, targetWidth, targetHeight );

		int sourceX = (int) ( ( targetWidth - preferredWidth ) * 0.5f );
		int sourceY = (int) ( ( targetHeight - preferredHeight ) * 0.5f );

		RenderTexture rt = RenderTexture.GetTemporary( preferredWidth, preferredHeight );
		RenderTexture activeRT = RenderTexture.active;

		try
		{
			Graphics.Blit( sourceTex, rt );
			RenderTexture.active = rt;

			Color32[] background = new Color32[targetWidth * targetHeight];
			for( int i = background.Length - 1; i >= 0; i-- )
				background[i] = fillColor;

			result = new Texture2D( targetWidth, targetHeight, format, options.generateMipmaps, options.linearColorSpace );
			result.SetPixels32( background );
			result.ReadPixels( new Rect( 0, 0, preferredWidth, preferredHeight ), sourceX, sourceY, false );
			result.Apply( options.generateMipmaps, options.markNonReadable );
		}
		catch( Exception e )
		{
			Debug.LogException( e );

			Object.Destroy( result );
			result = null;
		}
		finally
		{
			RenderTexture.active = activeRT;
			RenderTexture.ReleaseTemporary( rt );
		}

		return result;
	}

	public static Texture2D[] Slice( Texture sourceTex, int sliceTexWidth, int sliceTexHeight, TextureFormat format = TextureFormat.RGBA32, Options options = new Options() )
	{
		if( sourceTex == null )
			throw new ArgumentException( "Parameter 'sourceTex' is null!" );

		Texture2D[] result = null;

		int sourceWidth = sourceTex.width;
		int sourceHeight = sourceTex.height;

		float _1OverSourceWidth = 1f / sourceWidth;
		float _1OverSourceHeight = 1f / sourceHeight;

		RenderTexture rt = RenderTexture.GetTemporary( sliceTexWidth, sliceTexHeight );
		RenderTexture activeRT = RenderTexture.active;

		Vector4 sliceParameters = new Vector4( 0, 0, sliceTexWidth * _1OverSourceWidth, sliceTexHeight * _1OverSourceHeight );

		int uvOffsetId = Shader.PropertyToID( "_UVOffset" );
		if( sliceMaterial == null || sliceMaterial.Equals( null ) )
			sliceMaterial = new Material( Shader.Find( "Hidden/TextureOpsSliceShader" ) );

		try
		{
			result = new Texture2D[( sourceWidth / sliceTexWidth ) * ( sourceHeight / sliceTexHeight )];
			int resultIndex = 0;

			for( int y = sourceHeight; y - sliceTexHeight >= 0; y -= sliceTexHeight )
			{
				for( int x = 0; x + sliceTexWidth <= sourceWidth; x += sliceTexWidth )
				{
					sliceParameters.x = x * _1OverSourceWidth;
					sliceParameters.y = ( y - sliceTexHeight ) * _1OverSourceHeight;
					sliceMaterial.SetVector( uvOffsetId, sliceParameters );

					rt.DiscardContents();
					Graphics.Blit( sourceTex, rt, sliceMaterial );
					RenderTexture.active = rt;

					result[resultIndex] = new Texture2D( sliceTexWidth, sliceTexHeight, format, options.generateMipmaps, options.linearColorSpace );
					result[resultIndex].ReadPixels( new Rect( 0, 0, sliceTexWidth, sliceTexHeight ), 0, 0, false );
					result[resultIndex].Apply( options.generateMipmaps, options.markNonReadable );

					resultIndex++;
				}
			}
		}
		catch( Exception e )
		{
			Debug.LogException( e );

			for( int i = result.Length - 1; i >= 0; i-- )
				Object.Destroy( result[i] );

			result = null;
		}
		finally
		{
			RenderTexture.active = activeRT;
			RenderTexture.ReleaseTemporary( rt );
		}

		return result;
	}
	#endregion

	#region Helper Functions
	public static byte[] GetTextureBytesFromCopy( Texture2D sourceTex, bool isJpeg )
	{
		// Texture is marked as non-readable, create a readable copy and save it instead
		Debug.LogWarning( "Saving non-readable textures is slower than saving readable textures" );

		Texture2D sourceTexReadable = Scale( sourceTex, sourceTex.width, sourceTex.height, isJpeg ? TextureFormat.RGB24 : TextureFormat.RGBA32, new Options( false, false, false ) );
		if( sourceTexReadable == null )
			return null;

		try
		{
			return isJpeg ? sourceTexReadable.EncodeToJPG( 100 ) : sourceTexReadable.EncodeToPNG();
		}
		catch( Exception e )
		{
			Debug.LogException( e );
			return null;
		}
		finally
		{
			Object.DestroyImmediate( sourceTexReadable );
		}
	}
	#endregion

	#region Utility Functions
	private static void ScaleSize( ref int width, ref int height, int maxWidth, int maxHeight )
	{
		float aspect = (float) width / height;
		width = (int) ( aspect * maxHeight );
		height = (int) ( maxWidth / aspect );

		if( width <= maxWidth )
			height = maxHeight;
		else
			width = maxWidth;
	}

	private static bool IsJpeg( string imagePath )
	{
		if( string.IsNullOrEmpty( imagePath ) )
			return false;

		string pathLower = imagePath.ToLowerInvariant();
		return pathLower.EndsWith( ".jpeg", StringComparison.OrdinalIgnoreCase ) || pathLower.EndsWith( ".jpg", StringComparison.OrdinalIgnoreCase );
	}

	public static ImageProperties GetImageProperties( string imagePath )
	{
		if( !File.Exists( imagePath ) )
			throw new FileNotFoundException( "File not found at " + imagePath );

#if !UNITY_EDITOR && UNITY_ANDROID
		string value = AJC.CallStatic<string>( "GetImageProperties", Context, imagePath );
#elif !UNITY_EDITOR && UNITY_IOS
		string value = _TextureOps_GetImageProperties( imagePath );
#else
		string value = null;
#endif

		int width = 0, height = 0;
		string mimeType = null;
		ImageOrientation orientation = ImageOrientation.Unknown;
		if( !string.IsNullOrEmpty( value ) )
		{
			string[] properties = value.Split( '>' );
			if( properties != null && properties.Length >= 4 )
			{
				if( !int.TryParse( properties[0].Trim(), out width ) )
					width = 0;
				if( !int.TryParse( properties[1].Trim(), out height ) )
					height = 0;

				mimeType = properties[2].Trim();
				if( mimeType.Length == 0 )
				{
					string extension = Path.GetExtension( imagePath ).ToLowerInvariant();
					if( extension == ".png" )
						mimeType = "image/png";
					else if( extension == ".jpg" || extension == ".jpeg" )
						mimeType = "image/jpeg";
					else if( extension == ".gif" )
						mimeType = "image/gif";
					else if( extension == ".bmp" )
						mimeType = "image/bmp";
					else
						mimeType = null;
				}

				int orientationInt;
				if( int.TryParse( properties[3].Trim(), out orientationInt ) )
					orientation = (ImageOrientation) orientationInt;
			}
		}

		return new ImageProperties( width, height, mimeType, orientation );
	}

	public static VideoProperties GetVideoProperties( string videoPath )
	{
		if( !File.Exists( videoPath ) )
			throw new FileNotFoundException( "File not found at " + videoPath );

#if !UNITY_EDITOR && UNITY_ANDROID
		string value = AJC.CallStatic<string>( "GetVideoProperties", Context, videoPath );
#elif !UNITY_EDITOR && UNITY_IOS
		string value = _TextureOps_GetVideoProperties( videoPath );
#else
		string value = null;
#endif

		int width = 0, height = 0;
		long duration = 0L;
		float rotation = 0f;
		if( !string.IsNullOrEmpty( value ) )
		{
			string[] properties = value.Split( '>' );
			if( properties != null && properties.Length >= 4 )
			{
				if( !int.TryParse( properties[0].Trim(), out width ) )
					width = 0;
				if( !int.TryParse( properties[1].Trim(), out height ) )
					height = 0;
				if( !long.TryParse( properties[2].Trim(), out duration ) )
					duration = 0L;
				if( !float.TryParse( properties[3].Trim().Replace( ',', '.' ), NumberStyles.Float, CultureInfo.InvariantCulture, out rotation ) )
					rotation = 0f;
			}
		}

		if( rotation == -90f )
			rotation = 270f;

		return new VideoProperties( width, height, duration, rotation );
	}
	#endregion
}
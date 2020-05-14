# Unity Texture Ops Plugin

![SlidingPuzzleExample](screenshots/1.jpg)

**Forum Thread:** https://forum.unity.com/threads/texture-ops-a-basic-image-processing-plugin-for-unity-open-source.539065/

**[Support the Developer ☕](https://yasirkula.itch.io/unity3d)**

## ABOUT

This plugin helps you save/load textures and perform simple operations on them, like scale and slice. It also contains an example sliding puzzle project to demonstrate the slice operation.

### iOS Setup

iOS setup is normally handled automatically via a post processor script but, if you wish, you can execute the following steps to set up the plugin manually:

- set the value of **ENABLED** to *false* in *TOPostProcessBuild.cs*
- build your project
- insert `-framework MobileCoreServices -framework ImageIO` to the **Other Linker Flags** of *Unity-iPhone Target*:

![OtherLinkerFlags](screenshots/2.png)

## INSTALLATION

There are 4 ways to install this plugin:

- import [TextureOps.unitypackage](https://github.com/yasirkula/UnityTextureOps/releases) via *Assets-Import Package*
- clone/[download](https://github.com/yasirkula/UnityTextureOps/archive/master.zip) this repository and move the *Plugins* folder to your Unity project's *Assets* folder
- *(via Package Manager)* add the following line to *Packages/manifest.json*:
  - `"com.yasirkula.textureops": "https://github.com/yasirkula/UnityTextureOps.git",`
- *(via [OpenUPM](https://openupm.com))* after installing [openupm-cli](https://github.com/openupm/openupm-cli), run the following command:
  - `openupm add com.yasirkula.textureops`

## HOW TO

**NOTE:** functions that return a *Texture2D* or *Texture2D[]* may return *null*, if something goes wrong.

### A. Saving/Loading Textures

`bool TextureOps.SaveImage( Texture2D sourceTex, string imagePath )`: saves *sourceTex* as an image file. If *imagePath* ends with '*.jpeg*' or '*.jpg*', texture will be saved as JPEG, otherwise it will be saved as PNG. Returns *true*, if the image is saved successfully.

`bool TextureOps.SaveImage( byte[] sourceBytes, string imagePath )`: saves raw image bytes as an image file. Returns *true*, if the image is saved successfully.

`Texture2D TextureOps.LoadImage( byte[] imageBytes, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options )`: loads raw image bytes into a *Texture2D* and returns it.
- **format** specifies the format of the texture; RGB24 consumes less memory than RGBA32 but doesn't support transparency
- **options** determines the properties of the generated Texture2D. It has the following variables:
  - `bool generateMipmaps`: determines whether or not the texture will have mipmaps enabled
  - `bool linearColorSpace`: determines whether the texture will use linear color space or gamma color space. Gamma space is the Unity-specified default value
  - `bool markNonReadable`: determines whether or not the texture will be marked as non-readable. Such textures consume less memory but don't support read/write operations such as *GetPixels*/*SetPixels* and have slower *TextureOps.SaveImage* performance

`Texture2D TextureOps.LoadImage( string imagePath, int maxSize = -1, TextureOps.Options options )`: creates a *Texture2D* from the specified image file and returns it. On Android & iOS, if the orientation of the source image is rotated (e.g. when the image is captured with the camera in portrait mode), then its orientation will be corrected while loading the image into the texture.
- **maxSize** determines the maximum size of the returned Texture2D in pixels. Larger textures will be down-scaled. If untouched, its value will be set to *SystemInfo.maxTextureSize*. It is recommended to set a proper maxSize for better performance

### B. Texture Operations

**NOTE:** on some Android devices, these functions may not work correctly when called with a *sourceTex* that was created in the same frame. Therefore, if you'd like to call these functions immediately after *LoadImage*, consider instead waiting for at least one frame. You can use `yield return null;` in a coroutine to wait for one frame.

`Texture2D TextureOps.Crop( Texture2D, sourceTex, int leftOffset, int topOffset, int width, int height, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options )`: crops *sourceTex* and returns the cropped texture.

`Texture2D TextureOps.Scale( Texture2D sourceTex, int targetWidth, int targetHeight, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options )`: scales *sourceTex* to the specified size and returns the scaled texture. sourceTex's aspect ratio may not be preserved.

`Texture2D TextureOps.ScaleFill( Texture2D sourceTex, int targetWidth, int targetHeight, Color32 fillColor, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options )`: scales *sourceTex* to the specified size and returns the scaled texture. sourceTex's aspect ratio is preserved and blank space is filled with *fillColor*.

`Texture2D[] TextureOps.Slice( Texture2D sourceTex, int sliceTexWidth, int sliceTexHeight, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options )`: slices *sourceTex* into smaller textures and returns these textures in an array. sourceTex is sliced row-by-row, starting from top-left corner. Note that if a 100-pixel-wide texture is sliced into 30-pixel-wide textures, rightmost 10 pixels will be discarded. Likewise, if a 100-pixel-wide texture is sliced into 101-pixel-wide textures, the returned array will be empty.

### C. Utility Functions

`TextureOps.ImageProperties TextureOps.GetImageProperties( string imagePath )`: *[Android & iOS only]* returns an *ImageProperties* instance that holds the width, height, mime type and EXIF orientation information of an image file without creating a *Texture2D* object. Mime type will be *null*, if it can't be determined.

`TextureOps.VideoProperties TextureOps.GetVideoProperties( string videoPath )`: *[Android & iOS only]* returns a *VideoProperties* instance that holds the width, height, duration (in milliseconds) and rotation information of a video file. To play a video in correct orientation, you should rotate it by *rotation* degrees clockwise. For a 90-degree or 270-degree rotated video, values of *width* and *height* should be swapped to get the display size of the video.

## EXAMPLE CODE

The following code loads "*DESKTOP_DIR/image.jpeg*" into a Texture, applies *TextureOps.ScaleFill* to it and then saves it as "*DESKTOP_DIR/image_new.jpeg*". See *SlidePuzzleScene* demo scene for an example usage of *TextureOps.Slice*.

```csharp
void Start()
{
	string desktopDir = Environment.GetFolderPath( Environment.SpecialFolder.DesktopDirectory );

	Texture2D loadedImage = TextureOps.LoadImage( Path.Combine( desktopDir, "image.jpeg" ) );
	if( loadedImage != null )
	{
		Texture2D scaledImage = TextureOps.ScaleFill( loadedImage, 512, 512, Color.red );
		if( scaledImage != null )
			Debug.Log( "Save result: " + TextureOps.SaveImage( scaledImage, Path.Combine( desktopDir, "image_new.jpeg" ) ) );

		// Destroy procedural textures that are not needed anymore (otherwise, they'll continue consuming memory)
		DestroyImmediate( loadedImage );
		DestroyImmediate( scaledImage );
	}
}
```

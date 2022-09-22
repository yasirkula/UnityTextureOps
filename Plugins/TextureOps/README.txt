= Texture Ops (v1.2.1) =

Online documentation & example code available at: https://github.com/yasirkula/UnityTextureOps
E-mail: yasirkula@gmail.com


1. ABOUT
This plugin helps you save/load textures and perform simple operations on them, like scale and slice.


2. iOS SETUP
iOS setup is normally handled automatically via a post processor script but, if you wish, you can execute the following steps to set up the plugin manually:

- set the value of 'Automated Setup' to false at 'Project Settings/yasirkula/Texture Ops'
- build your project
- insert "-framework MobileCoreServices -framework ImageIO" to the "Other Linker Flags" of Unity-iPhone Target (and UnityFramework Target on Unity 2019.3 or newer)


3. SCRIPTING API
Please see the online documentation for a more in-depth documentation of the Scripting API: https://github.com/yasirkula/UnityTextureOps

//// SAVE-LOAD FUNCTIONS ////
bool TextureOps.SaveImage( Texture2D sourceTex, string imagePath ): saves sourceTex as an image file. If imagePath ends with '.jpeg' or '.jpg', texture will be saved as JPEG, otherwise it will be saved as PNG. Returns true, if the image is saved successfully.
bool TextureOps.SaveImage( byte[] sourceBytes, string imagePath ): saves raw image bytes as an image file. Returns true, if the image is saved successfully.

// format: specifies the format of the texture; RGB24 consumes less memory than RGBA32 but doesn't support transparency
// options: determines the properties of the generated Texture2D. It has the following variables:
//   - bool generateMipmaps: determines whether or not the texture will have mipmaps enabled
//   - bool linearColorSpace: determines whether the texture will use linear color space or gamma color space. Gamma space is the Unity-specified default value
//   - bool markNonReadable: determines whether or not the texture will be marked as non-readable. Such textures consume less memory but don't support read/write operations such as GetPixels/SetPixels and have slower TextureOps.SaveImage performance
Texture2D TextureOps.LoadImage( byte[] imageBytes, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options ): loads raw image bytes into a Texture2D and returns it.

// maxSize: determines the maximum size of the returned Texture2D in pixels. Larger textures will be down-scaled. If untouched, its value will be set to SystemInfo.maxTextureSize. It is recommended to set a proper maxSize for better performance
Texture2D TextureOps.LoadImage( string imagePath, int maxSize = -1, TextureOps.Options options ): creates a Texture2D from the specified image file and returns it.
async Task<Texture2D> TextureOps.LoadImageAsync( string imagePath, int maxSize = -1, TextureOps.Options options ): creates a Texture2D from the specified image file and returns it.

//// TEXTURE OPERATIONS ////
// NOTE: on some Android devices, these functions may not work correctly when called with a 'sourceTex' that was created in the same frame. Therefore, if you'd like to call these functions immediately after 'LoadImage', consider instead waiting for at least one frame. You can use `yield return null;` in a coroutine to wait for one frame.

Texture2D TextureOps.Crop( Texture2D, sourceTex, int leftOffset, int topOffset, int width, int height, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options ): crops sourceTex and returns the cropped texture.
Texture2D TextureOps.Scale( Texture2D sourceTex, int targetWidth, int targetHeight, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options ): scales sourceTex to the specified size and returns the scaled texture. sourceTex's aspect ratio may not be preserved.
Texture2D TextureOps.ScaleFill( Texture2D sourceTex, int targetWidth, int targetHeight, Color32 fillColor, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options ): scales sourceTex to the specified size and returns the scaled texture. sourceTex's aspect ratio is preserved and blank space is filled with fillColor.
Texture2D[] TextureOps.Slice( Texture2D sourceTex, int sliceTexWidth, int sliceTexHeight, TextureFormat format = TextureFormat.RGBA32, TextureOps.Options options ): slices sourceTex into smaller textures and returns these textures in an array. sourceTex is sliced row-by-row, starting from top-left corner.

//// UTILITY FUNCTIONS ////
TextureOps.ImageProperties TextureOps.GetImageProperties( string imagePath ): [Android & iOS only] returns an ImageProperties instance that holds the width, height, mime type and EXIF orientation information of an image file without creating a Texture2D object. Mime type will be null, if it can't be determined.
TextureOps.VideoProperties TextureOps.GetVideoProperties( string videoPath ): [Android & iOS only] returns a VideoProperties instance that holds the width, height, duration (in milliseconds) and rotation information of a video file.
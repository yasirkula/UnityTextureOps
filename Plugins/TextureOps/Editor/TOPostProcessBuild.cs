using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

public class TOPostProcessBuild
{
	private const bool ENABLED = true;

	[InitializeOnLoadMethod]
	public static void ValidatePlugin()
	{
		string jarPath = "Assets/Plugins/TextureOps/Android/TextureOps.jar";
		if( File.Exists( jarPath ) )
		{
			Debug.Log( "Deleting obsolete " + jarPath );
			AssetDatabase.DeleteAsset( jarPath );
		}
	}

#if UNITY_IOS
#pragma warning disable 0162
	[PostProcessBuild]
	public static void OnPostprocessBuild( BuildTarget target, string buildPath )
	{
		if( !ENABLED )
			return;

		if( target == BuildTarget.iOS )
		{
			string pbxProjectPath = PBXProject.GetPBXProjectPath( buildPath );

			PBXProject pbxProject = new PBXProject();
			pbxProject.ReadFromFile( pbxProjectPath );

#if UNITY_2019_3_OR_NEWER
			string targetGUID = pbxProject.GetUnityFrameworkTargetGuid();
#else
			string targetGUID = pbxProject.TargetGuidByName( PBXProject.GetUnityTargetName() );
#endif

			pbxProject.AddBuildProperty( targetGUID, "OTHER_LDFLAGS", "-framework MobileCoreServices" );
			pbxProject.AddBuildProperty( targetGUID, "OTHER_LDFLAGS", "-framework ImageIO" );

			File.WriteAllText( pbxProjectPath, pbxProject.WriteToString() );
		}
	}
#pragma warning restore 0162
#endif
}
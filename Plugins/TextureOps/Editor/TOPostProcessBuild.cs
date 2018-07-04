#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using UnityEditor.iOS.Xcode;
#endif

public class TOPostProcessBuild
{
	private const bool ENABLED = true;

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

			string targetGUID = pbxProject.TargetGuidByName( PBXProject.GetUnityTargetName() );

			pbxProject.AddBuildProperty( targetGUID, "OTHER_LDFLAGS", "-framework MobileCoreServices" );
			pbxProject.AddBuildProperty( targetGUID, "OTHER_LDFLAGS", "-framework ImageIO" );

			File.WriteAllText( pbxProjectPath, pbxProject.WriteToString() );
		}
	}
#pragma warning restore 0162
#endif
}
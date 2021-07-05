using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

public class TOPostProcessBuild
{
	[System.Serializable]
	public class Settings
	{
		private const string SAVE_PATH = "ProjectSettings/TextureOps.json";

		public bool AutomatedSetup = true;

		private static Settings m_instance = null;
		public static Settings Instance
		{
			get
			{
				if( m_instance == null )
				{
					try
					{
						if( File.Exists( SAVE_PATH ) )
							m_instance = JsonUtility.FromJson<Settings>( File.ReadAllText( SAVE_PATH ) );
						else
							m_instance = new Settings();
					}
					catch( System.Exception e )
					{
						Debug.LogException( e );
						m_instance = new Settings();
					}
				}

				return m_instance;
			}
		}

		public void Save()
		{
			File.WriteAllText( SAVE_PATH, JsonUtility.ToJson( this, true ) );
		}

#if UNITY_2018_3_OR_NEWER
		[SettingsProvider]
		public static SettingsProvider CreatePreferencesGUI()
		{
			return new SettingsProvider( "Project/yasirkula/Texture Ops", SettingsScope.Project )
			{
				guiHandler = ( searchContext ) => PreferencesGUI(),
				keywords = new System.Collections.Generic.HashSet<string>() { "Texture", "Ops" }
			};
		}
#endif

#if !UNITY_2018_3_OR_NEWER
		[PreferenceItem( "Texture Ops" )]
#endif
		public static void PreferencesGUI()
		{
			EditorGUI.BeginChangeCheck();

			Instance.AutomatedSetup = EditorGUILayout.Toggle( "Automated Setup", Instance.AutomatedSetup );

			if( EditorGUI.EndChangeCheck() )
				Instance.Save();
		}
	}

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
	[PostProcessBuild]
	public static void OnPostprocessBuild( BuildTarget target, string buildPath )
	{
		if( !Settings.Instance.AutomatedSetup )
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
#endif
}
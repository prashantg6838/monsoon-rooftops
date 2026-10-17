using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MonsoonRooftops.EditorTools
{
    public static class AndroidProjectSetup
    {
        private const string ApplicationId = "com.prashant.monsoonrooftops";

        [MenuItem("Monsoon/Apply Android Settings")]
        public static void Apply()
        {
            var target = NamedBuildTarget.Android;

            PlayerSettings.companyName = "Prashant";
            PlayerSettings.productName = "Monsoon Rooftops";
            PlayerSettings.SetApplicationIdentifier(target, ApplicationId);

            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            EditorUserBuildSettings.buildAppBundle = false; // APK for device testing; flip to true for release AAB
            Debug.Log("Monsoon Rooftops: Android settings applied (IL2CPP, ARM64, landscape, min API 23).");
        }
    }
}

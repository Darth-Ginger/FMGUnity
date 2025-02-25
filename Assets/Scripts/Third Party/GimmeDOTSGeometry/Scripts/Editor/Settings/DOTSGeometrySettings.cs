using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class DOTSGeometrySettings : ScriptableObject
    {

        public DegeneracyHandling degeneracies;
        public SafetyChecks safetyChecks;

        //The defines are reversed, so that the default case is SAFE and STRICT
        //This is so that the package is stable for a user that does not go into the project settings first
        //In the code #ifndef UNSAFE is used instead of #if SAFE so to speak
        public static string UNSAFE_DEGENERACIES = "GDG_UNSAFE_DEGENERACIES";
        public static string LENIENT_SAFETY_CHECKS = "GDG_LENIENT_SAFETY_CHECKS";


        internal static DOTSGeometrySettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DOTSGeometrySettings>("Assets/Settings/GimmeDOTSGeometry/DOTSGeometrySettings.asset");
            if(settings == null)
            {
                settings = ScriptableObject.CreateInstance<DOTSGeometrySettings>();

                settings.degeneracies = DegeneracyHandling.SAFE;
                settings.safetyChecks = SafetyChecks.STRICT;

                if (!Directory.Exists("Assets/Settings/GimmeDOTSGeometry"))
                {
                    Directory.CreateDirectory("Assets/Settings/GimmeDOTSGeometry/");
                }

                AssetDatabase.CreateAsset(settings, "Assets/Settings/GimmeDOTSGeometry/DOTSGeometrySettings.asset");
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    public static class DOTSGeometrySettingsRegister
    {

        private static bool RemoveDefine(string define, string[] defines, out string[] newDefines)
        {
            int defineIdx = -1;
            for(int i = 0; i < defines.Length; i++)
            {
                if (defines[i].Equals(define))
                {
                    defineIdx = i;
                    break;
                }
            }

            if (defineIdx < 0) {
                newDefines = null;
                return false; 
            }

            newDefines = new string[defines.Length - 1];
            int counter = 0;
            for(int i = 0; i < defines.Length; i++)
            {
                if(i != defineIdx)
                {
                    newDefines[counter] = defines[i];
                    counter++;
                }
            }
            return true;
        }

        private static bool AddDefine(string define, string[] defines, out string[] newDefines)
        {
            int defineIdx = -1;
            for (int i = 0; i < defines.Length; i++)
            {
                if (defines[i].Equals(define))
                {
                    defineIdx = i;
                    break;
                }
            }
            if(defineIdx >= 0)
            {
                newDefines = null;
                return false;
            }

            newDefines = new string[defines.Length + 1];
            Array.Copy(defines, newDefines, defines.Length);
            newDefines[defines.Length] = define;
            return true;
        }

        private static void HandleDegeneracies(DOTSGeometrySettings geometrySettings, BuildTargetGroup currentGroup)
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(currentGroup), out string[] defines);

            switch (geometrySettings.degeneracies)
            {
                case DegeneracyHandling.SAFE:
                    {
                        if (RemoveDefine(DOTSGeometrySettings.UNSAFE_DEGENERACIES, defines, out var newDefines))
                        {
                            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(currentGroup), newDefines);
                        }
                    }
                    break;
                case DegeneracyHandling.UNSAFE:
                    {
                        if (AddDefine(DOTSGeometrySettings.UNSAFE_DEGENERACIES, defines, out var newDefines))
                        {
                            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(currentGroup), newDefines);
                        }

                    }
                    break;
            }
        }

        private static void HandleSafetyChecks(DOTSGeometrySettings geometrySettings, BuildTargetGroup currentGroup)
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(currentGroup), out var defines);

            switch (geometrySettings.safetyChecks)
            {
                case SafetyChecks.LENIENT:
                    {
                        if (AddDefine(DOTSGeometrySettings.LENIENT_SAFETY_CHECKS, defines, out var newDefines))
                        {
                            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(currentGroup), newDefines);
                        }
                    }
                    break;
                case SafetyChecks.STRICT:
                    {
                        if (RemoveDefine(DOTSGeometrySettings.LENIENT_SAFETY_CHECKS, defines, out var newDefines))
                        {
                            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(currentGroup), newDefines);
                        }

                    }
                    break;
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/DOTS Geometry", SettingsScope.Project)
            {
                label = "DOTS Geometry",
                guiHandler = (searchContext) =>
                {
                    var settings = DOTSGeometrySettings.GetSerializedSettings();

                    var geometrySettings = settings.targetObject as DOTSGeometrySettings;

                    if (geometrySettings != null)
                    {
                        var currentGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

                        EditorGUI.BeginChangeCheck();
                        geometrySettings.degeneracies = (DegeneracyHandling)EditorGUILayout.EnumPopup(
                            new GUIContent("Degeneracy Handling", "How to deal with degenerate cases (triangles with colinear points, etc.) within the jobs?"),
                            geometrySettings.degeneracies);

                        if (EditorGUI.EndChangeCheck())
                        {
                            HandleDegeneracies(geometrySettings, currentGroup);
                        }

                        EditorGUI.BeginChangeCheck();

                        geometrySettings.safetyChecks = (SafetyChecks)EditorGUILayout.EnumPopup(
                            new GUIContent("Safety Checks", "How strictly should the input to methods be checked?"),
                            geometrySettings.safetyChecks);

                        if (EditorGUI.EndChangeCheck())
                        {
                            HandleSafetyChecks(geometrySettings, currentGroup);
                        }
                    }

                },
                keywords = new HashSet<string>(new[] { "geometry", "degeneracy", "degeneracies", "safety", "check", "checks" }),
            };
            return provider;
        }
    }
}

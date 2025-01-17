﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Editor;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input.Editor
{
    [CustomEditor(typeof(MixedRealityPointerProfile))]
    public class MixedRealityPointerProfileInspector : BaseMixedRealityToolkitConfigurationProfileInspector
    {
        private static readonly GUIContent ControllerTypeContent = new GUIContent("Controller Type", "The type of Controller this pointer will attach itself to at runtime.");
        private static readonly GUIContent MinusButtonContent = new GUIContent("-", "Remove Pointer Option");
        private static readonly GUIContent AddButtonContent = new GUIContent("+ Add a New Pointer Option", "Add Pointer Option");
        private static readonly GUIContent GazeCursorPrefabContent = new GUIContent("Gaze Cursor Prefab");
        private static readonly GUIContent RaycastLayerMaskContent = new GUIContent("Default Raycast LayerMasks");

        private const string ProfileTitle = "Pointer Settings";
        private const string ProfileDescription = "Pointers attach themselves onto controllers as they are initialized.";

        private SerializedProperty pointingExtent;
        private SerializedProperty pointingRaycastLayerMasks;
        private static bool showPointerOptionProperties = true;
        private SerializedProperty pointerOptions;

        private SerializedProperty debugDrawPointingRays;
        private SerializedProperty debugDrawPointingRayColors;
        private SerializedProperty gazeCursorPrefab;
        private SerializedProperty gazeProviderType;
        private SerializedProperty useHeadGazeOverride;
        private SerializedProperty isEyeTrackingEnabled;

        private static bool showGazeProviderProperties = true;
        private UnityEditor.Editor gazeProviderEditor;

        private SerializedProperty pointerMediator;
        private SerializedProperty primaryPointerSelector;

        protected override void OnEnable()
        {
            base.OnEnable();

            pointingExtent = serializedObject.FindProperty("pointingExtent");
            pointingRaycastLayerMasks = serializedObject.FindProperty("pointingRaycastLayerMasks");
            pointerOptions = serializedObject.FindProperty("pointerOptions");
            debugDrawPointingRays = serializedObject.FindProperty("debugDrawPointingRays");
            debugDrawPointingRayColors = serializedObject.FindProperty("debugDrawPointingRayColors");
            gazeCursorPrefab = serializedObject.FindProperty("gazeCursorPrefab");
            gazeProviderType = serializedObject.FindProperty("gazeProviderType");
            useHeadGazeOverride = serializedObject.FindProperty("useHeadGazeOverride");
            isEyeTrackingEnabled = serializedObject.FindProperty("isEyeTrackingEnabled");
            pointerMediator = serializedObject.FindProperty("pointerMediator");
            primaryPointerSelector = serializedObject.FindProperty("primaryPointerSelector");
        }

        public override void OnInspectorGUI()
        {
            if (!RenderProfileHeader(ProfileTitle, ProfileDescription, target, true, BackProfileType.Input))
            {
                return;
            }

            using (new EditorGUI.DisabledGroupScope(IsProfileLock((BaseMixedRealityProfile)target)))
            {
                serializedObject.Update();

                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(pointingExtent);
                EditorGUILayout.PropertyField(pointingRaycastLayerMasks, RaycastLayerMaskContent, true);
                EditorGUILayout.PropertyField(pointerMediator);
                EditorGUILayout.PropertyField(primaryPointerSelector);

                GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Gaze Settings", EditorStyles.boldLabel);
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(gazeCursorPrefab, GazeCursorPrefabContent);
                    EditorGUILayout.PropertyField(gazeProviderType);
                    EditorGUILayout.PropertyField(useHeadGazeOverride);
                    EditorGUILayout.PropertyField(isEyeTrackingEnabled);
                    EditorGUILayout.Space();

                    var gazeProvider = CameraCache.Main.GetComponent<IMixedRealityGazeProvider>();
                    CreateCachedEditor((Object)gazeProvider, null, ref gazeProviderEditor);

                    showGazeProviderProperties = EditorGUILayout.Foldout(showGazeProviderProperties, "Gaze Provider Settings", true, boldFoldout);
                    if (showGazeProviderProperties && !gazeProviderEditor.IsNull())
                    {
                        gazeProviderEditor.OnInspectorGUI();
                    }
                }

                EditorGUILayout.Space();
                showPointerOptionProperties = EditorGUILayout.Foldout(showPointerOptionProperties, "Pointer Options", true, boldFoldout);

                if (showPointerOptionProperties)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        RenderPointerList(pointerOptions);
                    }
                }


                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);
                {
                    EditorGUILayout.PropertyField(debugDrawPointingRays);
                    EditorGUILayout.PropertyField(debugDrawPointingRayColors, true);
                }

                serializedObject.ApplyModifiedProperties();
            }
        }

        protected override bool IsProfileInActiveInstance()
        {
            var profile = target as BaseMixedRealityProfile;
            return MixedRealityToolkit.IsInitialized && profile != null &&
                   MixedRealityToolkit.Instance.HasActiveProfile &&
                   MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile != null &&
                   profile == MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.PointerProfile;
        }

        private void RenderPointerList(SerializedProperty list)
        {
            var profile = target as MixedRealityPointerProfile;

            if (InspectorUIUtility.RenderIndentedButton(AddButtonContent, EditorStyles.miniButton))
            {
                pointerOptions.arraySize += 1;

                var newPointerOption = list.GetArrayElementAtIndex(list.arraySize - 1);
                var controllerType = newPointerOption.FindPropertyRelative("controllerType");
                var handedness = newPointerOption.FindPropertyRelative("handedness");
                var prefab = newPointerOption.FindPropertyRelative("pointerPrefab");
                var raycastLayerMask = newPointerOption.FindPropertyRelative("prioritizedLayerMasks");

                // Reset new entry
                controllerType.intValue = 0;
                handedness.intValue = 0;
                prefab.objectReferenceValue = null;
                raycastLayerMask.arraySize = 0;
            }

            if (list == null || list.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Create a new Pointer Option entry.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                IMixedRealityPointer pointer = null;
                Object pointerPrefab = null;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    Color prevColor = GUI.color;

                    var pointerOption = list.GetArrayElementAtIndex(i);
                    var controllerType = pointerOption.FindPropertyRelative("controllerType");
                    var handedness = pointerOption.FindPropertyRelative("handedness");
                    var prefab = pointerOption.FindPropertyRelative("pointerPrefab");
                    var prioritizedLayerMasks = pointerOption.FindPropertyRelative("prioritizedLayerMasks");

                    pointerPrefab = prefab.objectReferenceValue;
                    pointer = pointerPrefab.IsNull() ? null : ((GameObject)pointerPrefab).GetComponent<IMixedRealityPointer>();

                    // Display an error if the prefab doesn't have a IMixedRealityPointer Component
                    if (pointer == null)
                    {
                        InspectorUIUtility.DrawError($"The prefab associated with this pointer option needs an {typeof(IMixedRealityPointer).Name} component");

                        GUI.color = MixedRealityInspectorUtility.ErrorColor;
                    }
                    // if the prefab does have the component, provide a field to display and edit it's PrioritzedLayerMaskOverrides if it specifies a way to get it
                    else
                    {
                        // sync the pointer option with the prefab
                        if (pointer.PrioritizedLayerMasksOverride != null)
                        {
                            if (prioritizedLayerMasks.arraySize != pointer.PrioritizedLayerMasksOverride.Length)
                            {
                                prioritizedLayerMasks.arraySize = pointer.PrioritizedLayerMasksOverride.Length;
                            }
                            foreach (LayerMask mask in pointer.PrioritizedLayerMasksOverride)
                            {
                                SerializedProperty item = prioritizedLayerMasks.GetArrayElementAtIndex(prioritizedLayerMasks.arraySize - 1);
                                item.intValue = mask;
                            }
                        }

                        // if after syncing the the pointer option list is still empty, initialize with the global default
                        // sync the pointer option with the prefab
                        if (prioritizedLayerMasks.arraySize == 0)
                        {
                            for (int j = 0; j < pointingRaycastLayerMasks.arraySize; j++)
                            {
                                var mask = pointingRaycastLayerMasks.GetArrayElementAtIndex(j).intValue;

                                prioritizedLayerMasks.InsertArrayElementAtIndex(prioritizedLayerMasks.arraySize);
                                SerializedProperty item = prioritizedLayerMasks.GetArrayElementAtIndex(prioritizedLayerMasks.arraySize - 1);
                                item.intValue = mask;
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(prefab);
                        if (GUILayout.Button(MinusButtonContent, EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                        {
                            list.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    EditorGUILayout.PropertyField(controllerType, ControllerTypeContent);
                    EditorGUILayout.PropertyField(handedness);

                    // Ultimately sync the pointer prefab's value with the pointer option's
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(prioritizedLayerMasks, new GUIContent("Pointer Raycast LayerMasks"), true);
                    if (EditorGUI.EndChangeCheck() && pointer.PrioritizedLayerMasksOverride != null)
                    {
                        Undo.RecordObject(pointerPrefab, "Sync Pointer Prefab");
                        pointer.PrioritizedLayerMasksOverride = new LayerMask[prioritizedLayerMasks.arraySize];
                        for (int j = 0; j < prioritizedLayerMasks.arraySize; j++)
                        {
                            pointer.PrioritizedLayerMasksOverride[j] = prioritizedLayerMasks.GetArrayElementAtIndex(j).intValue;
                        }
                        
                        PrefabUtility.RecordPrefabInstancePropertyModifications(pointerPrefab);
                    }

                    GUI.color = prevColor;
                }
                EditorGUILayout.Space();
            }
        }
    }
}
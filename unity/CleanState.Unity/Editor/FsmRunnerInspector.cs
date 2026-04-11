// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using UnityEditor;
using UnityEngine;
using CleanState.Unity.Runtime;

namespace CleanState.Unity.Editor
{
    /// <summary>
    /// Custom inspector for FsmRunner that adds a button to open the debugger window
    /// and shows registered machine count at a glance.
    /// </summary>
    [CustomEditor(typeof(FsmRunner))]
    public sealed class FsmRunnerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    $"Registered machines: {FsmDebugRegistry.Count}",
                    MessageType.Info);
            }

            if (GUILayout.Button("Open FSM Debugger", GUILayout.Height(28)))
            {
                FsmGraphWindow.Open();
            }
        }
    }
}
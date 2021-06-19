using System;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.AnimatedValues;

namespace BennyKok.EventDrawer.Editor
{
    //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/UnityEventDrawer.cs

    [CustomPropertyDrawer(typeof(UnityEventBase), true)]
    public class AdvanceDropdownEventDrawer : UnityEventDrawer
    {
        public static SerializedObject copiedPersistentCalls;
        public static SerializedProperty targetPersistentCalls;
        public static int copySelectedIndex = -1;

        Dictionary<string, MState> states = new Dictionary<string, MState>();

        public class MState
        {
            public AnimBool visible;
            public int lastSelected;
        }

        public MState GetCurrentState(SerializedProperty property, bool defaultValue)
        {
            if (!states.TryGetValue(property.propertyPath, out var currentTabState))
            {
                var visible = new AnimBool();
                visible.speed = DrawerUtil.AnimSpeed;
                visible.valueChanged.AddListener(() =>
                {
                    DrawerUtil.RepaintInspector(property.serializedObject);
                });
                visible.value = defaultValue;

                currentTabState = new MState()
                {
                    visible = visible,
                };
                states.Add(property.propertyPath, currentTabState);
            }
            return currentTabState;
        }

        private void CopyPersistentCallProperty(string relative, SerializedProperty item, SerializedProperty copyItem)
        {
            SerializedProperty serializedProperty = item.FindPropertyRelative(relative);
            SerializedProperty serializedProperty1 = copyItem.FindPropertyRelative(relative);

            // p.s. not all serializedProperty type is checked, since some are only used in m_PersistentCalls.m_Calls
            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    serializedProperty.objectReferenceValue = serializedProperty1.objectReferenceValue;
                    break;
                case SerializedPropertyType.String:
                    serializedProperty.stringValue = serializedProperty1.stringValue;
                    break;
                case SerializedPropertyType.Enum:
                    serializedProperty.enumValueIndex = serializedProperty1.enumValueIndex;
                    break;
                case SerializedPropertyType.Integer:
                    serializedProperty.intValue = serializedProperty1.intValue;
                    break;
                case SerializedPropertyType.Float:
                    serializedProperty.floatValue = serializedProperty1.floatValue;
                    break;
                case SerializedPropertyType.Boolean:
                    serializedProperty.boolValue = serializedProperty1.boolValue;
                    break;
                case SerializedPropertyType.Color:
                    serializedProperty.colorValue = serializedProperty1.colorValue;
                    break;
                case SerializedPropertyType.Vector3:
                    serializedProperty.vector3Value = serializedProperty1.vector3Value;
                    break;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            m_Prop = property;

            // EditorGUI.indentLevel++;
            position = EditorGUI.IndentedRect(position);

            position.height = EditorGUIUtility.singleLineHeight;
            var temp = new GUIContent(label);

            SerializedProperty persistentCalls = property.FindPropertyRelative("m_PersistentCalls.m_Calls");
            var m_state = GetCurrentState(persistentCalls, property.isExpanded);
            var visible = m_state.visible;
            if (persistentCalls != null)
                temp.text += " (" + persistentCalls.arraySize + ")";

            EditorGUI.BeginChangeCheck();

            // visible.target = property.isExpanded;
            var tempBool = property.isExpanded;
            property.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(position, property.isExpanded, temp, null, (rect) =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Reset"), false, () =>
                {
                    persistentCalls.ClearArray();
                    property.serializedObject.ApplyModifiedProperties();
                });
                AddCopySelectOption(property.serializedObject, persistentCalls, menu, "Copy Selected", m_state.lastSelected);
                AddCopyAllOption(property, persistentCalls, menu);

                if (copiedPersistentCalls != null && copySelectedIndex == -1)
                    AddPasteAllOption(property, persistentCalls, menu);

                if (copiedPersistentCalls != null && copySelectedIndex > -1)
                    AddPasteSelectOption(property.serializedObject, persistentCalls, menu, persistentCalls.arraySize);

                menu.DropDown(rect);
            });

            if (EditorGUI.EndChangeCheck())
            {
                visible.target = property.isExpanded;
            }

            position.height = base.GetPropertyHeight(property, label) * visible.faded;
            position.y += EditorGUIUtility.singleLineHeight;
            if (DrawerUtil.BeginFade(visible, ref position))
            {
                var text = label.text;
                label.text = null;
                base.OnGUI(position, property, label);

                m_state.lastSelected = (m_LastSelectedIndexField.GetValue(this) as int?).Value;

                label.text = text;
            }
            DrawerUtil.EndFade();
            EditorGUI.EndFoldoutHeaderGroup();

            // EditorGUI.indentLevel--;
        }

        private void AddPasteSelectOption(SerializedObject property, SerializedProperty persistentCalls, GenericMenu menu, int targetIndex)
        {
            menu.AddItem(new GUIContent("Paste"), false, () =>
            {
                copiedPersistentCalls.Update();

                persistentCalls.InsertArrayElementAtIndex(targetIndex);
                CopyEvent(persistentCalls, targetIndex, copySelectedIndex);

                property.ApplyModifiedProperties();

                CleanUpAfterCopy();
                DrawerUtil.RepaintInspector(property);
            });
        }

        private void AddPasteAllOption(SerializedProperty property, SerializedProperty persistentCalls, GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Paste All"), false, () =>
            {
                copiedPersistentCalls.Update();

                persistentCalls.ClearArray();
                persistentCalls.arraySize = targetPersistentCalls.arraySize;
                for (int i = 0; i < persistentCalls.arraySize; i++)
                {
                    CopyEvent(persistentCalls, i, i);
                }

                property.serializedObject.ApplyModifiedProperties();

                CleanUpAfterCopy();
            });
        }

        public void AddCopySelectOption(SerializedObject property, SerializedProperty persistentCalls, GenericMenu menu, string label, int index)
        {
            menu.AddItem(new GUIContent(label), false, () =>
            {
                if (copiedPersistentCalls != null)
                    copiedPersistentCalls.Dispose();

                copySelectedIndex = index;
                copiedPersistentCalls = new SerializedObject(property.targetObject);
                targetPersistentCalls = copiedPersistentCalls.FindProperty(persistentCalls.propertyPath);
            });
        }

        public void AddCopyAllOption(SerializedProperty property, SerializedProperty persistentCalls, GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Copy All"), false, () =>
            {
                if (copiedPersistentCalls != null)
                    copiedPersistentCalls.Dispose();

                copySelectedIndex = -1;
                copiedPersistentCalls = new SerializedObject(property.serializedObject.targetObject);
                targetPersistentCalls = copiedPersistentCalls.FindProperty(persistentCalls.propertyPath);
            });
        }

        private void CleanUpAfterCopy()
        {
            copiedPersistentCalls.Dispose();
            copiedPersistentCalls = null;
            targetPersistentCalls = null;
            copySelectedIndex = -1;
        }

        private void CopyEvent(SerializedProperty persistentCalls, int i, int j)
        {
            var item = persistentCalls.GetArrayElementAtIndex(i);
            var copyItem = targetPersistentCalls.GetArrayElementAtIndex(j);

            CopyPersistentCallProperty("m_Target", item, copyItem);
            CopyPersistentCallProperty("m_TargetAssemblyTypeName", item, copyItem);
            CopyPersistentCallProperty("m_MethodName", item, copyItem);
            CopyPersistentCallProperty("m_Mode", item, copyItem);
            CopyPersistentCallProperty("m_CallState", item, copyItem);

            CopyPersistentCallProperty("m_Arguments.m_ObjectArgument", item, copyItem);
            CopyPersistentCallProperty("m_Arguments.m_ObjectArgumentAssemblyTypeName", item, copyItem);
            CopyPersistentCallProperty("m_Arguments.m_IntArgument", item, copyItem);
            CopyPersistentCallProperty("m_Arguments.m_FloatArgument", item, copyItem);
            CopyPersistentCallProperty("m_Arguments.m_StringArgument", item, copyItem);
            CopyPersistentCallProperty("m_Arguments.m_BoolArgument", item, copyItem);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var baseHeight = base.GetPropertyHeight(property, label);
            SerializedProperty persistentCalls = property.FindPropertyRelative("m_PersistentCalls.m_Calls");
            var m_state = GetCurrentState(persistentCalls, property.isExpanded);
            var visible = m_state.visible;

            if (property.propertyPath.Contains("Array"))
                return visible.target ? baseHeight + EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight;

            return baseHeight * visible.faded + EditorGUIUtility.singleLineHeight;
        }

        private const string kNoFunctionString = "No Function";

        //Persistent Listener Paths
        internal const string kInstancePath = "m_Target";
        internal const string kCallStatePath = "m_CallState";
        internal const string kArgumentsPath = "m_Arguments";
        internal const string kModePath = "m_Mode";
        internal const string kMethodNamePath = "m_MethodName";

        //ArgumentCache paths
        internal const string kFloatArgument = "m_FloatArgument";
        internal const string kIntArgument = "m_IntArgument";
        internal const string kObjectArgument = "m_ObjectArgument";
        internal const string kStringArgument = "m_StringArgument";
        internal const string kBoolArgument = "m_BoolArgument";
        internal const string kObjectArgumentAssemblyTypeName = "m_ObjectArgumentAssemblyTypeName";

        UnityEventBase m_DummyEvent;
        SerializedProperty m_Prop;
        SerializedProperty m_ListenersArray;

        static FieldInfo m_ListenersArrayField = typeof(UnityEventDrawer).GetField("m_ListenersArray",
            BindingFlags.NonPublic |
            BindingFlags.Instance);

        static FieldInfo m_DummyEventField = typeof(UnityEventDrawer).GetField("m_DummyEvent",
            BindingFlags.NonPublic |
            BindingFlags.Instance);

        static FieldInfo m_LastSelectedIndexField = typeof(UnityEventDrawer).GetField("m_LastSelectedIndex",
            BindingFlags.NonPublic |
            BindingFlags.Instance);

        static FieldInfo m_ReorderableListField = typeof(UnityEventDrawer).GetField("m_ReorderableList",
            BindingFlags.NonPublic |
            BindingFlags.Instance);

        static PropertyInfo mixedValueContentProp = typeof(EditorGUI).GetProperty("mixedValueContent",
            BindingFlags.NonPublic |
            BindingFlags.Static);

        static MethodInfo GetRowRectsMethod = typeof(UnityEventDrawer).GetMethod("GetRowRects",
            BindingFlags.NonPublic |
            BindingFlags.Instance);

        static MethodInfo BuildPopupListsMethod = typeof(UnityEventDrawer).GetMethod("BuildPopupList",
            BindingFlags.NonPublic |
            BindingFlags.Static);

        Rect[] GetRowRects(Rect rect)
        {
            return GetRowRectsMethod.Invoke(this, new object[] { rect }) as Rect[];
        }

        static GenericMenu BuildPopupList(Object target, UnityEventBase dummyEvent, SerializedProperty listener)
        {
            return BuildPopupListsMethod.Invoke(null, new object[] { target, dummyEvent, listener }) as GenericMenu;
        }

        static PersistentListenerMode GetMode(SerializedProperty mode)
        {
            return (PersistentListenerMode)mode.enumValueIndex;
        }

        protected override void SetupReorderableList(ReorderableList list)
        {
            base.SetupReorderableList(list);
            list.draggable = true;
            list.drawHeaderCallback = null;
            list.headerHeight = 0;
            list.elementHeight = EditorGUIUtility.singleLineHeight + 5;
        }

        protected override void DrawEvent(Rect rect, int index, bool isActive, bool isFocused)
        {
            m_ListenersArray = m_ListenersArrayField.GetValue(this) as SerializedProperty;
            m_DummyEvent = m_DummyEventField.GetValue(this) as UnityEventBase;

            var pListener = m_ListenersArray.GetArrayElementAtIndex(index);

            var m_state = GetCurrentState(m_ListenersArray, false);

            rect.y++;
            Rect[] subRects = GetRowRects(rect);
            Rect enabledRect = subRects[0];
            Rect goRect = subRects[1];
            Rect functionRect = subRects[2];
            Rect argRect = subRects[3];

            enabledRect.width -= 10;
            functionRect.x -= 10;
            functionRect.width += 10;

            // find the current event target...
            var callState = pListener.FindPropertyRelative(kCallStatePath);
            var mode = pListener.FindPropertyRelative(kModePath);
            var arguments = pListener.FindPropertyRelative(kArgumentsPath);
            var listenerTarget = pListener.FindPropertyRelative(kInstancePath);
            var methodName = pListener.FindPropertyRelative(kMethodNamePath);

            Color c = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            Event current = Event.current;
            var tempRect = rect;
            tempRect.x -= 20;
            if (tempRect.Contains(current.mousePosition) && current.type == EventType.ContextClick)
            {
                DrawerUtil.RepaintInspector(m_ListenersArray.serializedObject);

                GenericMenu menu = new GenericMenu();

                AddCopySelectOption(m_ListenersArray.serializedObject, m_ListenersArray, menu, "Copy", index);
                if (copiedPersistentCalls != null && copySelectedIndex > -1)
                    AddPasteSelectOption(m_ListenersArray.serializedObject, m_ListenersArray, menu, index + 1);

                menu.AddSeparator("");
                for (int i = 0; i < callState.enumDisplayNames.Length; i++)
                {
                    string state = (string)callState.enumDisplayNames[i];
                    int localI = i;
                    menu.AddItem(new GUIContent(state), callState.enumValueIndex == localI, () =>
                    {
                        callState.enumValueIndex = localI;
                        callState.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.ShowAsContext();

                current.Use();
            };

            // EditorGUI.PropertyField(enabledRect, callState, GUIContent.none);

            EditorGUI.BeginDisabledGroup(callState.enumValueIndex == 0);

            EditorGUI.BeginChangeCheck();
            {
                GUI.Box(enabledRect, GUIContent.none);
                EditorGUI.PropertyField(enabledRect, listenerTarget, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                    methodName.stringValue = null;
            }

            SerializedProperty argument;
            var modeEnum = GetMode(mode);
            //only allow argument if we have a valid target / method
            if (listenerTarget.objectReferenceValue == null || string.IsNullOrEmpty(methodName.stringValue))
                modeEnum = PersistentListenerMode.Void;

            switch (modeEnum)
            {
                case PersistentListenerMode.Float:
                    argument = arguments.FindPropertyRelative(kFloatArgument);
                    break;
                case PersistentListenerMode.Int:
                    argument = arguments.FindPropertyRelative(kIntArgument);
                    break;
                case PersistentListenerMode.Object:
                    argument = arguments.FindPropertyRelative(kObjectArgument);
                    break;
                case PersistentListenerMode.String:
                    argument = arguments.FindPropertyRelative(kStringArgument);
                    break;
                case PersistentListenerMode.Bool:
                    argument = arguments.FindPropertyRelative(kBoolArgument);
                    break;
                default:
                    argument = arguments.FindPropertyRelative(kIntArgument);
                    break;
            }

            var desiredArgTypeName = arguments.FindPropertyRelative(kObjectArgumentAssemblyTypeName).stringValue;
            var desiredType = typeof(Object);
            if (!string.IsNullOrEmpty(desiredArgTypeName))
                desiredType = Type.GetType(desiredArgTypeName, false) ?? typeof(Object);

            if (modeEnum == PersistentListenerMode.Object)
            {
                argRect.y = functionRect.y;
                argRect.width = 100;

                functionRect.width -= argRect.width;
                argRect.x = 4 + functionRect.x + functionRect.width;

                EditorGUI.BeginChangeCheck();
                var result = EditorGUI.ObjectField(argRect, GUIContent.none, argument.objectReferenceValue, desiredType, true);
                if (EditorGUI.EndChangeCheck())
                    argument.objectReferenceValue = result;
            }
            else if (modeEnum != PersistentListenerMode.Void && modeEnum != PersistentListenerMode.EventDefined)
            {
                if (modeEnum == PersistentListenerMode.Bool)
                {
                    argRect.y = functionRect.y;
                    argRect.width = EditorStyles.toggle.CalcSize(GUIContent.none).x;

                    functionRect.width -= argRect.width;
                    argRect.x = 4 + functionRect.x + functionRect.width;
                }
                else
                {
                    argRect.y = functionRect.y;
                    argRect.width = 100;

                    functionRect.width -= argRect.width;
                    argRect.x = 4 + functionRect.x + functionRect.width;
                }

                EditorGUI.PropertyField(argRect, argument, GUIContent.none);
            }

            using (new EditorGUI.DisabledScope(listenerTarget.objectReferenceValue == null))
            {
                EditorGUI.BeginProperty(functionRect, GUIContent.none, methodName);
                {
                    GUIContent buttonContent;
                    if (EditorGUI.showMixedValue)
                    {
                        buttonContent = mixedValueContentProp.GetValue(null, null) as GUIContent;
                    }
                    else
                    {
                        var buttonLabel = new StringBuilder();
                        if (listenerTarget.objectReferenceValue == null || string.IsNullOrEmpty(methodName.stringValue))
                        {
                            buttonLabel.Append(kNoFunctionString);
                        }
                        else if (!IsPersistantListenerValid(m_DummyEvent, methodName.stringValue, listenerTarget.objectReferenceValue, GetMode(mode), desiredType))
                        {
                            var instanceString = "UnknownComponent";
                            var instance = listenerTarget.objectReferenceValue;
                            if (instance != null)
                                instanceString = instance.GetType().Name;

                            buttonLabel.Append(string.Format("<Missing {0}.{1}>", instanceString, methodName.stringValue));
                        }
                        else
                        {
                            buttonLabel.Append(listenerTarget.objectReferenceValue.GetType().Name);

                            if (!string.IsNullOrEmpty(methodName.stringValue))
                            {
                                buttonLabel.Append(".");
                                if (methodName.stringValue.StartsWith("set_"))
                                    buttonLabel.Append(methodName.stringValue.Substring(4));
                                else
                                    buttonLabel.Append(methodName.stringValue);
                            }
                        }
                        buttonContent = new GUIContent(buttonLabel.ToString());
                    }

                    if (GUI.Button(functionRect, buttonContent, EditorStyles.popup))
                    {
                        var menu = BuildPopupList(listenerTarget.objectReferenceValue, m_DummyEvent, pListener);
                        new GenericAdvancedDropdown("Functions", menu).Dropdown(functionRect, 10);
                    }

                }
                EditorGUI.EndProperty();
            }
            GUI.backgroundColor = c;

            EditorGUI.EndDisabledGroup();
        }
    }


    public static class DrawerUtil
    {
        public static float AnimSpeed = 10f;
        private static Stack<Color> cacheColors = new Stack<Color>();

        public static bool BeginFade(AnimBool anim, ref Rect rect)
        {
            cacheColors.Push(GUI.color);
            GUI.BeginClip(rect);
            rect.x = 0;
            rect.y = 0;

            if ((double)anim.faded == 0.0)
                return false;
            if ((double)anim.faded == 1.0)
                return true;

            var c = GUI.color;
            c.a = anim.faded;
            GUI.color = c;

            if ((double)anim.faded != 0.0 && (double)anim.faded != 1.0)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                }

                GUI.FocusControl(null);
            }

            return (double)anim.faded != 0.0;
        }

        public static void EndFade()
        {
            GUI.EndClip();
            GUI.color = cacheColors.Pop();
        }

        public static void RepaintInspector(SerializedObject BaseObject)
        {
            foreach (var item in ActiveEditorTracker.sharedTracker.activeEditors)
                if (item.serializedObject == BaseObject)
                {
                    item.Repaint();
                    return;
                }
        }
    }
}
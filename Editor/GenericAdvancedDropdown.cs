using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// Little help from https://qiita.com/shogo281/items/fb24cf7d28f06822527e

namespace BennyKok.EventDrawer.Editor
{
    public class GenericAdvancedDropdown : AdvancedDropdown
    {
        private Dictionary<string, Entry> pathToActionMap = new Dictionary<string, Entry>();
        private Dictionary<int, Action> idToActionMap = new Dictionary<int, Action>();
        private string title = "Items";

        public struct Entry
        {
            public Action action;
            public Texture2D icon;
        }

#if UNITY_2021_2_OR_NEWER
        static PropertyInfo menuItemsField = typeof(GenericMenu).GetProperty("menuItems",
            BindingFlags.NonPublic |
            BindingFlags.Instance);
#else
        static FieldInfo menuItemsField = typeof(GenericMenu).GetField("menuItems",
            BindingFlags.NonPublic |
            BindingFlags.Instance);
#endif

        static FieldInfo menuItemContentField = typeof(GenericMenu).GetNestedType("MenuItem", BindingFlags.NonPublic |
            BindingFlags.Instance)?.GetField("content",
            BindingFlags.Public |
            BindingFlags.Instance);

        static FieldInfo menuItemMenuFunctionField = typeof(GenericMenu).GetNestedType("MenuItem", BindingFlags.NonPublic |
            BindingFlags.Instance)?.GetField("func",
            BindingFlags.Public |
            BindingFlags.Instance);

        static FieldInfo menuItemMenuFunction2Field = typeof(GenericMenu).GetNestedType("MenuItem", BindingFlags.NonPublic |
            BindingFlags.Instance)?.GetField("func2",
            BindingFlags.Public |
            BindingFlags.Instance);

        static FieldInfo menuItemUserDataField = typeof(GenericMenu).GetNestedType("MenuItem", BindingFlags.NonPublic |
            BindingFlags.Instance)?.GetField("userData",
            BindingFlags.Public |
            BindingFlags.Instance);

        static FieldInfo menuItemSeparatorField = typeof(GenericMenu).GetNestedType("MenuItem", BindingFlags.NonPublic |
            BindingFlags.Instance)?.GetField("separator",
            BindingFlags.Public |
            BindingFlags.Instance);

        public GenericAdvancedDropdown(AdvancedDropdownState state) : base(state) { }
        public GenericAdvancedDropdown() : base(new AdvancedDropdownState()) { }
        public GenericAdvancedDropdown(string title) : base(new AdvancedDropdownState())
        {
            this.title = title;
        }

        public GenericAdvancedDropdown(string title, GenericMenu menu, Func<GUIContent, Texture2D> iconCallback = null) : base(new AdvancedDropdownState())
        {
            this.title = title;

            var allItems = menuItemsField.GetValue(menu) as IEnumerable;
            foreach (var item in allItems)
            {
                var content = menuItemContentField.GetValue(item) as GUIContent;
                var function = menuItemMenuFunctionField.GetValue(item) as GenericMenu.MenuFunction;
                var function2 = menuItemMenuFunction2Field.GetValue(item) as GenericMenu.MenuFunction2;
                var userData = menuItemUserDataField.GetValue(item);
                var separator = menuItemSeparatorField.GetValue(item) as bool?;

                // Debug.Log(menuItemMenuFunctionField.GetValue(item).GetType());
                // Debug.Log(menuItemMenuFunction2Field.GetValue(item).GetType());

                if (string.IsNullOrEmpty(content.text)) continue;
                if (separator.HasValue && separator.Value) continue;
                if (function == null && function2 == null) continue;

                AddItem(content.text, () =>
                {
                    if (function != null) function();
                    if (function2 != null) function2(userData);
                }, iconCallback?.Invoke(content));
            }
        }

        public void Dropdown(Rect rect, int minLineCount = -1)
        {
            var originalHeight = rect.height;
            Vector2 size = new Vector2(rect.width, 100);

            if (minLineCount > 0)
            {
                size.y = minLineCount * EditorGUIUtility.singleLineHeight;
            }
            this.minimumSize = size;

            var r = new Rect(rect.position - new Vector2(0, size.y - originalHeight), size);
            Show(r);
        }

        public void ShowAsContext(int minLineCount = -1)
        {
            Vector2 size = new Vector2(200, 100);

            if (minLineCount > 0)
            {
                size.y = minLineCount * EditorGUIUtility.singleLineHeight;
            }
            this.minimumSize = size;

            var r = new Rect(Event.current.mousePosition - new Vector2(size.x / 2, size.y), size);
            Show(r);
        }

        public void AddItem(string label, Action action, Texture2D icon = null)
        {
            var tempLabel = label;
            var index = 0;
            while (pathToActionMap.ContainsKey(tempLabel))
            {
                index++;
                tempLabel = label + " " + index.ToString();
            }
            pathToActionMap.Add(tempLabel, new Entry()
            {
                action = action,
                icon = icon,
            });
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(title);

            foreach (var item in pathToActionMap)
            {
                var splitStrings = item.Key.Split('/');
                var parent = root;
                AdvancedDropdownItem lastItem = null;

                foreach (var str in splitStrings)
                {
                    var foundChildItem = parent.children.FirstOrDefault(item => item.name == str);

                    if (foundChildItem != null)
                    {
                        parent = foundChildItem;
                        lastItem = foundChildItem;
                        continue;
                    }

                    var child = new AdvancedDropdownItem(str);

                    parent.AddChild(child);

                    parent = child;
                    lastItem = child;
                }

                if (lastItem != null && !idToActionMap.ContainsKey(lastItem.id))
                {
                    lastItem.icon = item.Value.icon;
                    idToActionMap.Add(lastItem.id, item.Value.action);
                }
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);

            if (idToActionMap.TryGetValue(item.id, out var action))
            {
                action();
            }
        }
    }
}
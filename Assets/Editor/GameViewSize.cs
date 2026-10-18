using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Sets the Game view to a fixed resolution (for checking the HUD on phone and tablet
    /// screen shapes). Unity keeps this API internal, so it is reached by reflection.
    /// </summary>
    public static class GameViewSize
    {
        public static void Set(int width, int height)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                if ((int)st.GetProperty("width").GetValue(size) == width && (int)st.GetProperty("height").GetValue(size) == height)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.ToObject(kindType, 1), width, height, $"{width}x{height}" });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = total;
            }
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameViewType);
            var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            select.Invoke(window, new object[] { index, null });
        }
    }
}

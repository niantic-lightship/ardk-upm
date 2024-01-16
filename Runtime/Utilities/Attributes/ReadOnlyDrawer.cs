// Copyright 2022-2024 Niantic.
#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Attributes
{
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    internal class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            string valueStr;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    valueStr = prop.intValue.ToString();
                    break;

                case SerializedPropertyType.Boolean:
                    valueStr = prop.boolValue.ToString();
                    break;

                case SerializedPropertyType.Float:
                    valueStr = prop.floatValue.ToString("0.000");
                    break;

                case SerializedPropertyType.Vector3:
                    valueStr = prop.vector3Value.ToString();
                    break;

                case SerializedPropertyType.Quaternion:
                    valueStr = prop.quaternionValue.ToString();
                    break;

                case SerializedPropertyType.String:
                    valueStr = prop.stringValue;
                    break;

                case SerializedPropertyType.Enum:
                    var selectedValue = prop.intValue;
                    var enumValues = (int[])System.Enum.GetValues(fieldInfo.FieldType);

                    StringBuilder valueSb = new StringBuilder();
                    for (int i = 0; i < enumValues.Length; i++)
                    {
                        if ((enumValues[i] & selectedValue) != 0)
                            valueSb.AppendFormat("{0} | ", prop.enumNames[i]);
                    }

                    if (valueSb.Length == 0)
                    {
                        valueStr = "None";
                    }
                    else
                    {
                        // Trim off trailing | and spaces
                        valueSb.Remove(valueSb.Length - 3, 3);
                        valueStr = valueSb.ToString();
                    }

                    break;

                case SerializedPropertyType.ObjectReference:
                    valueStr =
                        prop.objectReferenceValue != null
                            ? prop.objectReferenceValue.ToString()
                            : "None (Game Object)";
                    break;

                default:
                    valueStr = "(Unsupported value type)";
                    break;
            }

            EditorGUI.LabelField(position, label, new GUIContent(valueStr));
        }
    }
}
#endif

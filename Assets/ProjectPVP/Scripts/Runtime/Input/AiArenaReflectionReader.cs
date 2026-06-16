using System;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaReflectionReader
    {
        internal static bool ReadBoolProperty(MonoBehaviour behaviour, string propertyName, bool fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(bool)
                    ? (bool)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static int ReadIntProperty(MonoBehaviour behaviour, string propertyName, int fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(int)
                    ? (int)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static int ReadIntField(MonoBehaviour behaviour, string fieldName, int fallback)
        {
            try
            {
                var field = behaviour.GetType().GetField(fieldName);
                return field != null && field.FieldType == typeof(int)
                    ? (int)field.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static string ReadStringField(MonoBehaviour behaviour, string fieldName, string fallback)
        {
            try
            {
                var field = behaviour.GetType().GetField(fieldName);
                if (field == null)
                {
                    return fallback;
                }

                object value = field.GetValue(behaviour);
                if (value == null)
                {
                    return fallback;
                }

                var idField = value.GetType().GetField("id");
                if (idField != null && idField.FieldType == typeof(string))
                {
                    return (string)idField.GetValue(value) ?? fallback;
                }

                return value.ToString();
            }
            catch
            {
                return fallback;
            }
        }

        internal static float ReadFloatProperty(MonoBehaviour behaviour, string propertyName, float fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(float)
                    ? (float)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static string ReadStringProperty(MonoBehaviour behaviour, string propertyName, string fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(string)
                    ? (string)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static int ReadEnumAsIntProperty(MonoBehaviour behaviour, string propertyName, int fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                if (property == null)
                {
                    return fallback;
                }

                object value = property.GetValue(behaviour);
                if (value == null)
                {
                    return fallback;
                }

                return value.GetType().IsEnum ? Convert.ToInt32(value) : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static Rect ReadRectProperty(MonoBehaviour behaviour, string propertyName, Rect fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(Rect)
                    ? (Rect)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static Vector2 ReadVector2Property(MonoBehaviour behaviour, string propertyName, Vector2 fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(Vector2)
                    ? (Vector2)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        internal static GameObject ReadGameObjectProperty(MonoBehaviour behaviour, string propertyName, GameObject fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(GameObject)
                    ? (GameObject)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}

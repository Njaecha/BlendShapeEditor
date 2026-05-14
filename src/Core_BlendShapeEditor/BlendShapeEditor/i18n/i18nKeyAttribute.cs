using System;

namespace BlendShapeEditor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class i18nKeyAttribute : Attribute
    {
        public string Key { get; }
        public i18nKeyAttribute(string key) => Key = key;
    }
}
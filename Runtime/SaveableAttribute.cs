using System;

namespace Buck.SaveAsync
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SaveableAttribute : Attribute
    {
        public SaveableAttribute()
        {
        }
    }
}

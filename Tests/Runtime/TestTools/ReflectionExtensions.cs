using System;
using System.Linq;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// These are extensions to configure the SaveManager in potentially nonstandard ways, required in order to test it.
    /// Could be placed inside SaveManager if it is appropriate to expose a public API for these functions, rather than a testing-only API.
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Set the static file handler used by the SaveManager singleton.
        /// </summary>
        /// <param name="newFileHander"></param>
        public static void SetCustomFileHandler(FileHandler newFileHander)
        {
            if (!newFileHander)
            {
                throw new ArgumentNullException(nameof(newFileHander));
            }
            
            var fileHandlerField = typeof(SaveManager)
                .GetField("m_fileHandler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (fileHandlerField == null)
            {
                throw new InvalidOperationException("Could not find static private field 'm_fileHandler' on SaveManager.");
            }
            fileHandlerField.SetValue(null, newFileHander);
        }
    }
}
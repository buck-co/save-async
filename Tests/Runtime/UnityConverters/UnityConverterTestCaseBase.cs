using Newtonsoft.Json;
using Newtonsoft.Json.UnityConverters;

namespace Buck.SaveAsync.Tests
{
    public class UnityConverterTestCaseBase : TestCaseBase
    {       
        protected override void SetupSaveManager(FileHandler withFileHandler)
        {
            base.SetupSaveManager(withFileHandler);
            // ensure we are using the default Json-for-Unity converters
            var jsonSettings = UnityConverterInitializer.defaultUnityConvertersSettings;
            JsonConvert.DefaultSettings = () => jsonSettings;
        }
    }
}
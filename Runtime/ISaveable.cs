// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

namespace Buck.SaveAsync
{
    /// <summary>
    /// Allows an object to be saved and loaded via the SaveManager class.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// This is a unique string used to identify the object when saving and loading.
        /// If you choose to use a Guid, it is recommended that it is backed by a
        /// serialized byte array that does not change.
        /// </summary>
        /// <remarks>
        /// This is invoked off Unity's main thread, cannot use thread unsafe APIs.
        /// </remarks>
        public string Key { get; }
        
        /// <summary>
        /// This is the file name where this object's data will be saved.
        /// It is recommended to use a static class to store file paths as strings to avoid typos.
        /// </summary>
        /// <remarks>
        /// This is invoked on Unity's main thread, safe to use thread unsafe APIs.
        /// </remarks>
        public string Filename { get; }
        
        /// <summary>
        /// This is used by the SaveManager class to capture the state of a saveable object.
        /// Typically this is a struct defined by the ISaveable implementing class.
        /// The contents of the struct could be created at the time of saving, or cached in a variable.
        /// </summary>
        /// <remarks>
        /// This is invoked off Unity's main thread, cannot use thread unsafe APIs.
        /// </remarks>
        object CaptureState();
        
        /// <summary>
        /// This is used by the SaveManager class to restore the state of a saveable object.
        /// This will be called any time the game is loaded, so you may want to consider
        /// also using this method to initialize any fields that are not saved (i.e. "resetting the object").
        /// </summary>
        /// <remarks>
        /// This is invoked on Unity's main thread, safe to use thread unsafe APIs.
        /// </remarks>
        void RestoreState(object state);
    }
}

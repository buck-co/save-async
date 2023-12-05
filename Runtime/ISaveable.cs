using System;

namespace Buck.DataManagement
{
    /// <summary>
    /// Allows an object to be saved and loaded via the DataManager class.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// This property should be backed by a serialized byte array that does not change.
        /// This is used to identify the object when saving and loading.
        /// </summary>
        public Guid Guid { get; }
        
        /// <summary>
        /// This is the file name where this object's data will be saved.
        /// It is recommended to use a static class to store file paths as strings to avoid typos.
        /// </summary>
        public string FileName { get; }
        
        /// <summary>
        /// This is used by the DataManager class to capture the state of a saveable object.
        /// Typically this is a struct defined by the ISaveable implementing class.
        /// The contents of the struct could be created at the time of saving, or cached in a variable.
        /// Load the sample project from the BUCK Data Management package in the Unity Package Manager
        /// to see an example of both approaches.
        /// </summary>
        object CaptureState();
        
        /// <summary>
        /// This is used by the DataManager class to restore the state of a saveable object.
        /// This will be called any time the game is loaded, so you may want to consider
        /// also using this method to initialize any fields that are not saved (i.e. "resetting the object").
        /// </summary>
        void RestoreState(object state);
    }
}

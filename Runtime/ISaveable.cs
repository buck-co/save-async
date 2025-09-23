// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

namespace Buck.SaveAsync
{
    /// <summary>
    /// Allows an object to be saved and loaded via the SaveManager class using a strongly-typed state.
    /// Implementations should define a serializable struct or class for TState.
    /// </summary>
    /// <typeparam name="TState">A serializable type representing this object's save data.</typeparam>
    public interface ISaveable<TState>
    {
        /// <summary>
        /// This is a unique string used to identify the object when saving and loading.
        /// Often this can just be the name of the class if there is only one instance, like "EnemyManager" or "Player"
        /// If you choose to use a Guid, it is recommended that it is backed by a
        /// serialized byte array that does not change.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// This is the file name where this object's data will be saved.
        /// It is recommended to use a static class to store file paths as strings to avoid typos.
        /// </summary>
        string Filename { get; }
        
        /// <summary>
        /// This is the current version of this ISaveable's data structure.
        /// If you change the TState structure, increment this version number.
        /// Note: For now, this will invalidate all existing save data for this ISaveable.
        /// In the future, we may add support for data migration.
        /// </summary>
        int FileVersion { get; }

        /// <summary>
        /// This is used by the SaveManager class to capture the state of a saveable object.
        /// Typically this is a struct defined by the ISaveable implementing class.
        /// The contents of the struct could be created at the time of saving, or cached in a variable.
        /// </summary>
        TState CaptureState();

        /// <summary>
        /// This is used by the SaveManager class to restore the state of a saveable object.
        /// This will be called any time the game is loaded, so you need to handle cases where the "state" parameter is null.
        /// In cases where the state is null, you should initialize the object to a default state.
        /// You may also consider using this method to initialize any fields that are not saved (i.e. "resetting the object").
        /// </summary>
        void RestoreState(TState state);
    }
}
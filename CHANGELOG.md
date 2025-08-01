# Changelog

## [0.8.3] - 2025-07-30
- Added .meta files back for GitHub files since their absence was causing errors in Unity due to package folders being immutable.

## [0.8.2] - 2025-07-15

- It's now possible to call Load() without loading actual data. When the bool ignoreSaveData is true, save files will be ignored and RestoreState() will be passed a null value. This can be useful when working in the Unity Editor or if you want RestoreState() to use default values.

## [0.8.1] - 2025-07-14

- Fixed an issue where calling SaveManager.Load() multiple times with different filenames would result in ISaveables being restored to "null" by mistake.

## [0.8.0] - 2025-07-14

- **Breaking Change**: RestoreState() will now be called on all registered ISaveables, regardless of whether they are found in save data. That means they need to handle a null state in their RestoreState() method, which should be used to set an ISaveable's default state.
- The Delete() and Erase() methods will now automatically call the Load() method internally to refresh the state of all ISaveables. This behavior can be overridden by setting restoreDefaultSaveState to false in either method.
- Updated the included Samples to reflect the changes in the last few versions.
- Added a few comments throughout for clarity.

## [0.7.0] - 2025-07-09

- **Breaking Change**: FileHandler.Delete() now accepts a CancellationToken parameter for consistency with other async methods. Overloads without CancellationToken have been added for convenience.
- Added comprehensive path validation to FileHandler to prevent directory traversal attacks and ensure paths are well-formed.
- FileHandler.WriteFile() now automatically creates any missing directory structure before writing files, eliminating the need for manual directory creation.
- Added convenience method overloads to FileHandler for WriteFile(), ReadFile(), Erase(), and Delete() that don't require a CancellationToken parameter.
- Fixed SaveManager.Exists() method to correctly return bool instead of void.
- Added Initialize() calls to SaveManager.RegisterSaveable() and SaveManager.Exists() to prevent potential null reference exceptions when called before Awake().
- Optimized XOR encryption performance by eliminating string concatenation in favor of char array manipulation.
- Added password validation to Encryption class to ensure passwords are not null or empty.
- Fixed potential null reference issue in Singleton class by using explicit null checks.
- Improved error handling in FileHandler.ReadFile() to catch UnauthorizedAccessException and IOException.
- Added comprehensive XML documentation to ValidatePath() and all method overloads.
- FileHandler.ValidatePath() now throws ArgumentException with descriptive messages for invalid paths.
- Fixed typo in Singleton class XML documentation ("property" instead of "propriety").
- Improved SaveAsyncMenu editor utility with cross-platform support using EditorUtility.RevealInFinder() instead of Process.Start().
- SaveAsyncMenu now displays the full path in confirmation dialogs and includes comprehensive error handling.
- Added SaveAsyncMenu.ClearEditorSaveFiles() method to selectively delete only editor test files with '_editor' suffix.
- SaveAsyncMenu class is now properly marked as static and includes XML documentation.

## [0.6.0] - 2025-07-09

- The FileHandler class will now add the suffix "_editor" via the FilenameSuffix string property to any files saved from the Unity Editor. This property is protected and can be overridden by child FileHandler classes.
- The FileHandler class now has a protected FilenameExtension string property that will be added to the end of filenames. It is set to ".dat" by default, but can be overridden by child FileHandler classes.
- FileHanlder.GetPath() has been replaced by the protected GetPartialPath() method (which is the filename or path with the filename suffix and file extension) and the FileHandler.GetFullPath() method (which appends the persistent data path to the return value of the GetPartialPath() method). These can be breaking changes in some instances, which is why the minor version has been incremented.
- Fixed a few issues in CHANGELOG.md. Also, the reason version 0.5.0 was skipped was due to a previous 0.5.0 being released and rolled back.

## [0.4.4] - 2025-07-09

- The variable m_persistentDataPath in the FileHandler class is now protected rather than private, allowing child classes to override its value. This can be helpful for programmatically adding prefixes or suffixes to paths or filenames.


## [0.4.3] - 2024-08-20

- Converted the SaveManager.Exists method and the default FileHandler method to be async. This isn't necessary for typical file I/O but can be useful in the case of custom file handlers that have a wait time associated with checking for file availability.

## [0.4.2] - 2024-07-30

- Added the ability to disable background threads to help debug issues.

## [0.4.1] - 2024-07-16

- Added additional exception handling for malformed JSON data.

## [0.4.0] - 2024-07-16

- The ISaveable interface Guid property ISaveable.Guid has been replaced with a string property called ISaveable.Key. This is a breaking change, but it enables the option to use either a unique string key or store a Guid as a string, rather than using Guids and serialized byte arrays exclusively.
- Added some much needed error checking to validate ISaveable keys when loading data. Previously, if a key was not found in the registered saveables dictionary, it would throw an unhandled exception.

## [0.3.2] - 2024-07-09

- The default FileHandler now inherits from ScriptableObject and can be overridden. This can be useful in scenarios where files should not be saved using local file IO (such as cloud saves) or when a platform-specific save API must be used.

## [0.3.1] - 2024-01-20

- Renamed the project to "Save Async"

## [0.3.0] - 2023-12-11

- Fixed lots of potential concurrency issues and race conditions.
- Fixed an issue where spamming output could cause errors.
- Simplified DataManager internals to be more DRY.
- Added XML comments to all public DataManager API methods.
- Improved loading performance by reducing thread swapping.
- FileHandler methods now return a Task rather than an Awaitable which is necessary to support the async File API methods.
- Added more error checking throughout.
- Removed AES encryption for now until more work and testing can be done.

## [0.2.0] - 2023-11-28

- Added basic support for JSON writing and reading using Newtonsoft Json.NET
- Basic XOR encryption has been added. AES is still WIP.

## [0.1.0] - 2023-11-22

- Initial commit.
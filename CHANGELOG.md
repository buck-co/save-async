# Changelog

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
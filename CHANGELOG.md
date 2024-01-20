# Changelog

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
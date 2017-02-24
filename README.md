# GoldenHammer

### Compiling GoldenHammer

Windows: `.\build.cmd`  
Linux: `./build.sh`  
macOS: `./build.sh`  

### Publishing a Release

1. Write draft release notes for the release in `DRAFT_RELEASE.md`  
    > If you have [Github-changelog-generator](https://skywinder.github.io/github-changelog-generator/) installed, use [this script](https://gist.github.com/TomGillen/1dbca055a3d582178af40402e8ae15a8) to create `DRAFT_RELEASE.md`.
2. Once happy with the release, call `.\build TagRelease` to create release tag. Push this tag via `git push --follow-tags` to publish the release.  
3. Use [this script](https://gist.github.com/TomGillen/62ee9572cd19b40c471f66591e246f9b) to convert these tags into Github releases.
4. Rebuild via `.\build` to create a new NuGet package with updated release metadata.
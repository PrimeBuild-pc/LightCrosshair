# Third-Party Notices

## NvAPIWrapper.Net

**License:** GNU Lesser General Public License v3.0 (LGPL-3.0)
**Copyright:** Copyright (C) 2017-2020 Soroush Falahati
**Repository:** https://github.com/falahati/NvAPIWrapper
**NuGet:** https://www.nuget.org/packages/NvAPIWrapper.Net

LightCrosshair uses NvAPIWrapper.Net for NVIDIA GPU driver integration (frame rate limiting via DRS and digital vibrance control). NvAPIWrapper.Net is used as-is without modification.

The full LGPL-3.0 license text is available at: https://www.gnu.org/licenses/lgpl-3.0.html

Per LGPL-3.0 requirements:
- NvAPIWrapper.Net is used as an unmodified library
- The source code of NvAPIWrapper.Net is available at the repository above
- LightCrosshair does not modify NvAPIWrapper.Net source code

## AMD ADLX SDK

**License:** AMD ADLX SDK License Agreement (Proprietary)
**Repository:** https://gpuopen.com/adlx/
**Documentation:** https://gpuopen.com/manuals/adlx/

LightCrosshair references the AMD ADLX SDK for AMD GPU driver integration. The ADLX SDK is used for AMD GPU detection and capability probing.

Note: Full AMD Chill and FreeSync control via ADLX requires the ADLXCSharpBind C++/CLI wrapper, which is not bundled in this release. AMD color management uses the existing ADL2 API integration.

AMD ADLX SDK License: Proprietary AMD license. Distribution of compiled binaries is permitted. Source code redistribution is not permitted.

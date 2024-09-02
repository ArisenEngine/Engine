#pragma once

#ifdef CPPDYNAMICLIBRARYTEMPLATE_EXPORTS

#define CPP_SHARP_TEST_DLL   __declspec( dllexport )

#else

#define CPP_SHARP_TEST_DLL   __declspec( dllimport )

#endif
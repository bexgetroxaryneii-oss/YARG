// YARG iOS port: stand-ins for BASS functions that only exist in the Windows build.
//
// ManagedBass declares these, and IL2CPP on iOS links every P/Invoke directly, so the
// link fails with "Undefined symbols: _BASS_GetDSoundObject, _BASS_GetEAXParameters,
// _BASS_SetEAXParameters" without them. YARG never calls them on iOS; if anything
// ever does, they report failure the same way BASS does when a feature is missing.

#include <stddef.h>

typedef unsigned int DWORD;
typedef int BOOL;

void *BASS_GetDSoundObject(DWORD object)
{
    (void) object;
    return NULL;
}

BOOL BASS_GetEAXParameters(DWORD *env, float *vol, float *decay, float *damp)
{
    (void) env; (void) vol; (void) decay; (void) damp;
    return 0;
}

BOOL BASS_SetEAXParameters(int env, float vol, float decay, float damp)
{
    (void) env; (void) vol; (void) decay; (void) damp;
    return 0;
}

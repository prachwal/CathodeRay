/* stdint.h — typy o stałej szerokości (model celu: int 16 bitów, long 32, long long 64). */
#ifndef _STDINT_H
#define _STDINT_H
typedef signed char int8_t;
typedef unsigned char uint8_t;
typedef int int16_t;
typedef unsigned int uint16_t;
typedef long int32_t;
typedef unsigned long uint32_t;
typedef long long int64_t;
typedef unsigned long long uint64_t;
typedef int intptr_t;
typedef unsigned int uintptr_t;
typedef long long intmax_t;
typedef unsigned long long uintmax_t;
#define INT8_MIN (-128)
#define INT8_MAX 127
#define UINT8_MAX 255
#define INT16_MIN (-32767 - 1)
#define INT16_MAX 32767
#define UINT16_MAX 65535
#define INT32_MIN (-2147483647L - 1)
#define INT32_MAX 2147483647L
#define UINT32_MAX 4294967295UL
#endif

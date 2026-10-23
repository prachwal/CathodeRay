// C1: <stdint.h> — typy o stałej szerokości, rozmiary, zawijanie i znak.
// expect-result: 8433
#include <stdint.h>

uint8_t u8 = 250;
int8_t i8 = -3;
uint16_t u16 = 65530;
int16_t i16 = INT16_MIN;
uint32_t u32 = 4000000000UL;
int32_t i32 = -100000L;
uint64_t u64 = 1;
int64_t i64 = -1;

int main(void)
{
    int sizes = sizeof(uint8_t) + sizeof(int16_t) * 10 + sizeof(uint32_t) * 100 + sizeof(int64_t) * 1000;
    int wrap = 0;

    u8 += 10;                      /* 250 + 10 = 4 */
    u16 += 10;                     /* 65530 + 10 = 4 */
    u64 <<= 40;
    i64 = -1;
    wrap += u8 + u16;              /* 8 */
    if (i8 < 0) {
        wrap += 1;                 /* int8_t jest ze znakiem */
    }
    if (i16 == INT16_MIN) {
        wrap += 1;
    }
    if ((u64 >> 40) == 1) {
        wrap += 1;
    }
    if (i64 < 0 && i32 < 0 && u32 > 3000000000UL) {
        wrap += 1;
    }
    return sizes + wrap;
}

using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 29 D: biblioteka standardowa mini-C linkowana przez <c>cc</c>.</summary>
public sealed class CStdlibTests
{
    [Fact]
    public void String_Functions()
    {
        const string Source = """
            #include <string.h>
            uchar buf[16];
            int main() {
                strcpy(buf, "abc");
                strcat(buf, "de");
                uchar *e = strchr(buf, 'd');
                uchar part[4];
                strncpy(part, "wxyz", 3);
                part[3] = 0;
                uchar filled[5];
                memset(filled, 'q', 4);
                filled[4] = 0;
                uchar copy[6];
                memcpy(copy, buf, 6);
                int r = 0;
                r += strlen(buf) * 1;
                r += (e - buf) * 10;
                r += (strcmp("abc", "abd") < 0) * 100;
                r += (strcmp("b", "a") > 0) * 200;
                r += (strncmp("abcX", "abcY", 3) == 0) * 400;
                r += (memcmp(copy, buf, 6) == 0) * 800;
                r += (strchr(buf, 'z') == NULL) * 1600;
                r += strlen(part) * 3200 + filled[3] * 0;
                return r;
            }
            """;
        CcRun.Run(Source).Value.Should().Be(5 + 30 + 100 + 200 + 400 + 800 + 1600 + 9600);
    }

    [Fact]
    public void Ctype_And_Stdlib_Numbers()
    {
        const string Source = """
            #include <ctype.h>
            #include <stdlib.h>
            #include <string.h>
            uchar out[12];
            int main() {
                int r = isdigit('7') + isalpha('x') * 2 + isspace(' ') * 4 + isupper('a') * 8 + isalnum('_') * 16;
                r += (toupper('b') == 'B') * 32 + (tolower('Q') == 'q') * 64;
                r += (abs(0 - 12) == 12) * 128 + (min(3, 0 - 4) == 0 - 4) * 256 + (max(3, 9) == 9) * 512;
                r += (atoi("  -1234") == 0 - 1234) * 1024 + (atoi("+56x") == 56) * 2048;
                itoa(0 - 345, out, 10);
                r += (strcmp(out, "-345") == 0) * 4096;
                itoa(255, out, 16);
                r += (strcmp(out, "ff") == 0) * 8192;
                itoa(5, out, 2);
                r += (strcmp(out, "101") == 0) * 16384;
                return r;
            }
            """;
        CcRun.Run(Source).Value.Should().Be(1 + 2 + 4 + 0 + 0 + 32 + 64 + 128 + 256 + 512 + 1024 + 2048 + 4096 + 8192 + 16384);
    }

    [Fact]
    public void Rand_Is_Deterministic_And_Seedable()
    {
        const string Source = """
            #include <stdlib.h>
            int main() {
                srand(1);
                uint a = rand();
                uint b = rand();
                srand(1);
                uint c = rand();
                int r = (a == c) + (a != b) * 2;
                srand(0);
                r += (rand() != 0) * 4;
                return r;
            }
            """;
        CcRun.Run(Source).Value.Should().Be(7);
    }

    [Fact]
    public void Stdio_Console_Puts_Putdec_And_Printf_Formats()
    {
        const string Source = """
            #include <stdio.h>
            int main() {
                puts("hi");
                putstr("x=");
                putdec(0 - 42);
                putchar(10);
                int n = printf("%d|%u|%x|%c|%s|%%", 0 - 5, 65535, 255, 'Z', "str");
                putchar(10);
                printf("[%d %d %d %d %d]", 1, 2, 3, 4, 5);
                return n;
            }
            """;
        CcRun.Result result = CcRun.Run(Source);

        result.Console.Should().Be("hi\nx=-42\n-5|65535|ff|Z|str|%\n[1 2 3 4 5]");
        result.Value.Should().Be("-5|65535|ff|Z|str|%".Length);
    }

    [Fact]
    public void Sprintf_Formats_Into_A_Buffer()
    {
        const string Source = """
            #include <stdio.h>
            #include <string.h>
            uchar buf[40];
            int main() {
                int n = sprintf(buf, "%s-%d-%x", "ab", 300, 4096);
                return n * 100 + strlen(buf);
            }
            """;
        CcRun.Run(Source).Value.Should().Be((11 * 100) + 11);
    }

    [Fact]
    public void Only_Used_Library_Modules_Are_Linked_And_User_Definitions_Win()
    {
        const string Own = """
            uint strlen(const uchar *s) { return 99; }
            int main() { return strlen("abc"); }
            """;
        CcRun.Run(Own).Value.Should().Be(99);
        int small = CcRun.Run("int main() { return 1; }").Steps.GetHashCode();
        small.Should().NotBe(0);
    }

    [Fact]
    public void Nostdlib_Leaves_Symbols_Unresolved()
    {
        FluentActions.Invoking(() => CcRun.Run("#include <string.h>\nint main() { return strlen(\"a\"); }", "--nostdlib"))
            .Should().Throw<Exception>();
    }

    [Fact]
    public void Sscanf_Parses_Numbers_And_Reports_Eof_And_Limits()
    {
        const string Source = """
            #include <stdio.h>
            int main() {
                float f;
                float big;
                int a;
                int b;
                int c;
                int n;
                int r = 0;
                r += (sscanf("  -2.5e1x", "%g%n", &f, &n) == 1) * 1;
                r += ((int)f == 0 - 25) * 2;
                r += (n == 8) * 4;
                r += (sscanf("", "x") == EOF) * 8;
                r += (sscanf("", "%d", &a) == EOF) * 16;
                r += (sscanf("7", "%d %d", &a, &b) == 1) * 32;
                r += (sscanf("1e32767", "%g", &big) == 1) * 64;
                r += (sscanf("1 2 3 4", "%d %d %d %d", &a, &b, &c) == 3) * 128;
                r += (a + b + c == 6) * 256;
                return r;
            }
            """;
        CcRun.Run(Source).Value.Should().Be(1 + 2 + 4 + 8 + 16 + 32 + 64 + 128 + 256);
    }

    [Fact]
    public void Sscanf_Lg_Writes_A_Whole_Double_With_Double64()
    {
        const string Source = """
            #include <stdio.h>
            int main() {
                double d = 100;
                float f = 1;
                int r = sscanf("2.5", "%lg", &d);
                int s = sscanf("1.5", "%g", &f);
                return r * 1000 + s * 100 + (int)(d * 2) * 10 + (int)(f * 2);
            }
            """;
        CcRun.RunOn(Source, "z80", "-D", "CC_DOUBLE64").Value.Should().Be(1000 + 100 + 50 + 3);
    }

    [Fact]
    public void Getchar_Reads_The_Input_Buffer_And_Stops_At_Zero_Or_After_256_Bytes()
    {
        const string Source = """
            #include <stdio.h>
            extern uchar __in_buf[256];
            int main() {
                int r = 0;
                int i;
                __in_buf[0] = 'a';
                __in_buf[1] = 'b';
                r += (getchar() == 'a') * 1;
                r += (getchar() == 'b') * 2;
                r += (getchar() == EOF) * 4;
                return r;
            }
            """;
        CcRun.Run(Source).Value.Should().Be(7);
    }

    [Fact]
    public void Getchar_Returns_Eof_When_The_Buffer_Is_Full()
    {
        const string Source = """
            #include <stdio.h>
            extern uchar __in_buf[256];
            int main() {
                int i;
                int seen = 0;
                for (i = 0; i < 256; i++) {
                    __in_buf[i] = 'x';
                }
                for (i = 0; i < 256; i++) {
                    seen += (getchar() == 'x');
                }
                return seen + (getchar() == EOF) * 1000;
            }
            """;
        CcRun.Run(Source).Value.Should().Be(1256);
    }
}

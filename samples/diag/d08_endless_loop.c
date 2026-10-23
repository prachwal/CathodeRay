// Pętla bez końca: `cathode run --max-steps` kończy się kodem 2 i komunikatem o limicie.
// expect-exit: 2
// expect-stderr: step limit
int main(void)
{
    uchar n = 0;
    for (;;) {
        n++;
    }
    return n;
}

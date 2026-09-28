; Instrukcje i tryby specyficzne dla 65C02, porównywane z ca65 --cpu 65C02.
ptr     = $20

.include "part0.inc"
.include "part1.inc"
        trb ptr
        inc a
        dec
        phx
        ply
done:   rts
table:  .word start, skip, done

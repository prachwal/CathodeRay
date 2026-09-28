SEGMENT "CODE"
start: LD A,1
JP done
SEGMENT "DATA"
val: DEFB 9
BSS
buf: DEFS 1
SEGMENT "CODE"
done: LD A,(val)
RET

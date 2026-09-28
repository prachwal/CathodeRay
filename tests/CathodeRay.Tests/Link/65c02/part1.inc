        jmp (table,x)
skip:   bbr0 ptr, start
        bbs7 ptr, done
        rmb3 ptr
        smb5 ptr
        tsb $1234

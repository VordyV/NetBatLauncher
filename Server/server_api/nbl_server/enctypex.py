import base64
import hashlib
import time

# Code from Luigi Auriemma's enctypex_decoder.c
# It's kind of sloppy in parts, but it works. Unless there's some issues then it'll probably not change any longer.
class EncTypeX:
    def __init__(self):
        return

    def decrypt(self, key, validate, data):
        if not key or not validate or not data:
            return None

        encxkey = bytearray([0] * 261)
        data = self.init(encxkey, key, validate, data)
        self.func6(encxkey, data, len(data))

        return data

    def encrypt(self, key, validate, data):
        if not key or not validate or not data:
            return None

        # Convert data from strings to byte arrays before use or else it'll raise an error
        key = bytearray(key)
        validate = bytearray(validate)

        # Add room for the header
        tmp_len = 20
        data = bytearray(tmp_len) + data

        keylen = len(key)
        vallen = len(validate)
        rnd = ~int(time.time())

        for i in range(tmp_len):
            rnd = (rnd * 0x343FD) + 0x269EC3
            data[i] = (rnd ^ key[i % keylen] ^ validate[i % vallen]) & 0xff

        header_len = 7
        data[0] = (header_len - 2) ^ 0xec
        data[1] = 0x00
        data[2] = 0x00
        data[header_len - 1] = (tmp_len - header_len) ^ 0xea

        header = data[:tmp_len]  # The header of the data gets chopped off in init(), so save it
        encxkey = bytearray([0] * 261)
        data = self.init(encxkey, key, validate, data)
        self.func6e(encxkey, data, len(data))

        # Reappend header that we saved earlier before returning to make the complete buffer
        return header + data


    def init(self, encxkey, key, validate, data):
        data_len = len(data)

        if data_len < 1:
            return None

        header_len = (data[0] ^ 0xec) + 2
        if data_len < header_len:
            return None

        data_start = (data[header_len - 1] ^ 0xea)
        if data_len < (header_len + data_start):
            return None

        data = self.enctypex_funcx(encxkey, bytearray(key), bytearray(validate), data[header_len:], data_start)
        return data[data_start:]


    def enctypex_funcx(self, encxkey, key, validate, data, datalen):
        keylen = len(key)

        for i in range(datalen):
            validate[(key[i % keylen] * i) & 7] ^= validate[i & 7] ^ data[i]

        self.func4(encxkey, validate, 8)
        return data

    def func4(self, encxkey, id, idlen):
        if idlen < 1:
            return

        for i in range(256):
            encxkey[i] = i

        n1 = 0
        n2 = 0
        for i in range(255,-1,-1):
            t1, n1, n2 = self.func5(encxkey, i, id, idlen, n1, n2)
            t2 = encxkey[i]
            encxkey[i] = encxkey[t1]
            encxkey[t1] = t2

        encxkey[256] = encxkey[1]
        encxkey[257] = encxkey[3]
        encxkey[258] = encxkey[5]
        encxkey[259] = encxkey[7]
        encxkey[260] = encxkey[n1 & 0xff]

    def func5(self, encxkey, cnt, id, idlen, n1, n2):
        if cnt == 0:
            return 0, n1, n2

        mask = 1
        doLoop = True
        if cnt > 1:
            while doLoop:
                mask = (mask << 1) + 1
                doLoop = mask < cnt

        i = 0
        tmp = 0
        doLoop = True
        while doLoop:
            n1 = encxkey[n1 & 0xff] + id[n2]
            n2 += 1

            if n2 >= idlen:
                n2 = 0
                n1 += idlen

            tmp = n1 & mask

            i += 1
            if i > 11:
                tmp %= cnt

            doLoop = tmp > cnt

        return tmp, n1, n2

    def func6(self, encxkey, data, data_len):
        for i in range(data_len):
            data[i] = self.func7(encxkey, data[i])
        return len(data)

    def func7(self, encxkey, d):
        a = encxkey[256]
        b = encxkey[257]
        c = encxkey[a]
        encxkey[256] = (a + 1) & 0xff
        encxkey[257] = (b + c) & 0xff

        a = encxkey[260]
        b = encxkey[257]
        b = encxkey[b]
        c = encxkey[a]
        encxkey[a] = b

        a = encxkey[259]
        b = encxkey[257]
        a = encxkey[a]
        encxkey[b] = a

        a = encxkey[256]
        b = encxkey[259]
        a = encxkey[a]
        encxkey[b] = a

        a = encxkey[256]
        encxkey[a] = c

        b = encxkey[258]
        a = encxkey[c]
        c = encxkey[259]
        b = (a + b) & 0xff
        encxkey[258] = b

        a = b
        c = encxkey[c]
        b = encxkey[257]
        b = encxkey[b]
        a = encxkey[a]
        c = (b + c) & 0xff
        b = encxkey[260]
        b = encxkey[b]
        c = (b + c) & 0xff
        b = encxkey[c]
        c = encxkey[256]
        c = encxkey[c]
        a = (a + c) & 0xff
        c = encxkey[b]
        b = encxkey[a]
        encxkey[260] = d

        c ^= b ^ d
        encxkey[259] = c

        return c

    def func6e(self, encxkey, data, data_len):
        for i in range(data_len):
            data[i] = self.func7e(encxkey, data[i])
        return len(data)

    def func7e(self, encxkey, d):
        a = encxkey[256]
        b = encxkey[257]
        c = encxkey[a]
        encxkey[256] = (a + 1) & 0xff
        encxkey[257] = (b + c) & 0xff

        a = encxkey[260]
        b = encxkey[257]
        b = encxkey[b]
        c = encxkey[a]
        encxkey[a] = b

        a = encxkey[259]
        b = encxkey[257]
        a = encxkey[a]
        encxkey[b] = a

        a = encxkey[256]
        b = encxkey[259]
        a = encxkey[a]
        encxkey[b] = a

        a = encxkey[256]
        encxkey[a] = c

        b = encxkey[258]
        a = encxkey[c]
        c = encxkey[259]
        b = (a + b) & 0xff
        encxkey[258] = b

        a = b
        c = encxkey[c]
        b = encxkey[257]
        b = encxkey[b]
        a = encxkey[a]
        c = (b + c) & 0xff
        b = encxkey[260]
        b = encxkey[b]
        c = (b + c) & 0xff
        b = encxkey[c]
        c = encxkey[256]
        c = encxkey[c]
        a = (a + c) & 0xff
        c = encxkey[b]
        b = encxkey[a]
        c ^= b ^ d
        encxkey[260] = c
        encxkey[259] = d

        return c
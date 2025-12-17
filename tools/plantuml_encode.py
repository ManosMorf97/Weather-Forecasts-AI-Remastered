import sys
import zlib

ENCODE_TABLE = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_"

def encode6bit(b):
    return ENCODE_TABLE[b & 0x3F]

def append3bytes(b1, b2, b3):
    c1 = b1 >> 2
    c2 = ((b1 & 0x3) << 4) | (b2 >> 4)
    c3 = ((b2 & 0xF) << 2) | (b3 >> 6)
    c4 = b3 & 0x3F
    return encode6bit(c1) + encode6bit(c2) + encode6bit(c3) + encode6bit(c4)

def plantuml_encode(data: bytes) -> str:
    compressor = zlib.compressobj(level=9, wbits=-15)
    compressed = compressor.compress(data) + compressor.flush()
    res = []
    i = 0
    length = len(compressed)
    while i < length:
        b1 = compressed[i]
        b2 = compressed[i+1] if i+1 < length else 0
        b3 = compressed[i+2] if i+2 < length else 0
        res.append(append3bytes(b1, b2, b3))
        i += 3
    return ''.join(res)


def main():
    if len(sys.argv) < 2:
        print('Usage: plantuml_encode.py <puml-file>')
        sys.exit(2)
    path = sys.argv[1]
    with open(path, 'rb') as f:
        text = f.read()
    print(plantuml_encode(text))

if __name__ == '__main__':
    main()

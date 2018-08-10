using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class SpriteObject
    {
        private Ppu _ppu;

        public byte[] SpriteData = new byte[4];
        public byte Ypos { get { return SpriteData[0]; } }
        public byte TileNumber { get { return SpriteData[1]; } }
        public byte Palette { get { return (byte)(SpriteData[2] & 0x3); } }
        public bool BehindBG { get { return (SpriteData[2] & 0x20) == 0x20; } }
        public bool FlipX { get { return (SpriteData[2] & 0x40) == 0x40; } }
        public bool FlipY { get { return (SpriteData[2] & 0x80) == 0x80; } }
        public byte Xpos { get { return SpriteData[3]; } }
        public byte TileData0;
        public byte TileData1;
        public bool isSprite0 = false;

        public SpriteObject(Ppu ppu)
        {
            _ppu = ppu;
        }



        public void ClearSprite()
        {
            SpriteData = Enumerable.Repeat<byte>(0xFF, SpriteData.Length).ToArray();
            isSprite0 = false;
        }

        public void LoadTiledata(int scanline, int spriteTableAddress, bool largeSprites)
        {

            if (largeSprites)
            {
                int row = FlipY ? 15 - (scanline - Ypos) : scanline - Ypos;
                int tileAddress = (TileNumber & 1) != 0 ? 0x1000 : 0x0;
                if (row > 7)
                {
                    row += 8;
                }
                tileAddress |= ((TileNumber & 0xFE) * 0x10) + row;

                TileData0 = _ppu.ReadPpuMemory(tileAddress);
                TileData1 = _ppu.ReadPpuMemory(tileAddress + 8);
            }
            else
            {
                int row = FlipY ? 7 - (scanline - Ypos) : scanline - (Ypos);
                int tileAddress = ((TileNumber * 0x10) + row) | spriteTableAddress;
                TileData0 = _ppu.ReadPpuMemory(tileAddress);
                TileData1 = _ppu.ReadPpuMemory(tileAddress + 8);
            }
        }

        public byte GetPixel(int pixelpos)
        {
            int pixel = 0x10;
            if(!FlipX)
            {
                pixel |= TileData0 >> (7 - pixelpos) & 1;
                pixel |= ((TileData1 >> (7 - pixelpos)) & 1) << 1;
                pixel |= Palette << 2;
            }
            else
            {
                pixel |= (TileData0 >> pixelpos) & 1;
                pixel |= ((TileData1 >> pixelpos) & 1) << 1;
                pixel |= Palette << 2;
            }

            return (byte)pixel;
        }


    }
}

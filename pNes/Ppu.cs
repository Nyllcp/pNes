using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Ppu
    {
        private Cart _cart;
        private Core _core;

        private uint[] PalleteRGBlookup = new uint[0x40]{
                0x7C7C7C,0x0000FC,0x0000BC,0x4428BC,0x940084,0xA80020,0xA81000,0x881400,0x503000,0x007800,0x006800,0x005800,0x004058,0x000000,0x000000,0x000000,
                0xBCBCBC,0x0078F8,0x0058F8,0x6844FC,0xD800CC,0xE40058,0xF83800,0xE45C10,0xAC7C00,0x00B800,0x00A800,0x00A844,0x008888,0x000000,0x000000,0x000000,
                0xF8F8F8,0x3CBCFC,0x6888FC,0x9878F8,0xF878F8,0xF85898,0xF87858,0xFCA044,0xF8B800,0xB8F818,0x58D854,0x58F898,0x00E8D8,0x787878,0x000000,0x000000,
                0xFCFCFC,0xA4E4FC,0xB8B8F8,0xD8B8F8,0xF8B8F8,0xF8A4C0,0xF0D0B0,0xFCE0A8,0xF8D878,0xD8F878,0xB8F8B8,0xB8F8D8,0x00FCFC,0xF8D8F8,0x000000,0x000000
        };

        const int nesWidth = 256;
        const int nesHeight = 240;

        private byte[] oam = new byte[0x100];
        private byte[] secondaryOam = new byte[0x20];
        private byte[] vram = new byte[0x800];
        private byte[] scanlinebuffer = new byte[256];
        private uint[] _frame = new uint[nesWidth * nesHeight];
        private byte[] paletteRam = new byte[0x20];

        private byte ppuCtrl;
        private byte ppuMask;
        private byte oamAddr;
        private byte lastWritten;
        private byte xScroll;
        private byte yScroll;
        private byte readBuffer;

        private byte tileAttribute;
        private byte bufferTileAttribute;
        private int tileData0;
        private int tileData1;

        private int tilePointer;
        private int ppuAddress;
        private int tempPpuAddress;
        private int spriteTableAdress = 0x0000;
        private int backgroundTableAdress = 0x0000;
        private int vramAddressIncrement = 1;
        private int ppuCycles = 0;
        private int currentScanline = 0;
        private int fineX;

        private bool largeSprites = false;
        private bool vblank_NMI = false;
        private bool grayScale = false;
        private bool showLeftBg = false;
        private bool showLeftSprite = false;
        private bool bgEnabled = false;
        private bool spritesEnabled = false;
        private bool spriteOverflow = false;
        private bool sprite0Hit = false;
        private bool inVblank = false;
        private bool addressLatch = false;
        private bool oddFrame = false;
        private bool frameReady = false;
        private bool sprite0evaluated = false;
        

        public bool FrameReady { get { bool value = frameReady; frameReady = false; return value; } }

        public uint[] Frame { get { return _frame; } }

        public Ppu(Cart cart, Core core)
        {
            _cart = cart;
            _core = core;
        }

        public void Tick()
        {

            int currentDot = ppuCycles % 341;
            ppuCycles++;

            FetchTimer(currentDot);
            BgRenderer(currentDot);
            
            if(currentDot == 1) SpriteEvalution();
            if(currentDot == 256)SpriteRenderer();

            if (currentScanline == 241 && currentDot == 1)
            {
                inVblank = true;
                frameReady = true;
                if(vblank_NMI)
                {
                    _core.NonMaskableIntterupt();
                }
            }
            if (currentScanline == 261 && currentDot == 1)
            {
                inVblank = false;
                sprite0Hit = false;
                frameReady = false;
                if (bgEnabled || spritesEnabled)
                {
                    oddFrame = !oddFrame;
                    
                }

            }
            if (currentScanline == 261 && currentDot >= 280 && currentDot <= 304)
            {
                if (bgEnabled || spritesEnabled)
                {
                    ppuAddress = tempPpuAddress;
                    if (oddFrame && currentDot == 304)
                        ppuCycles++;
                }
               
            }
            if (currentDot == 340)
            {
                if(currentScanline <= 239)
                {
                    for (int i = 0; i < scanlinebuffer.Length; i++)
                    {     
                        _frame[i + (currentScanline * nesWidth)] = PalleteRGBlookup[paletteRam[scanlinebuffer[i]]];
                    }
                }
                currentScanline++;
                if (currentScanline > 261)
                {
                    currentScanline = 0;
                    ppuCycles = 0;
                    
                }

            }
        }

        private void SpriteEvalution()
        {
            if (spritesEnabled && currentScanline <= 239)
            {
                secondaryOam = new byte[0x20];
                int numberOfSprites = 0;
                for (int i = 0; i < 0xFF; i += 4)
                {
                    byte ypos = oam[i];
                    if (currentScanline >= (ypos + 1) && currentScanline <= ((ypos + 1) + (largeSprites ? 15 : 7)) && ypos != 0)
                    {
                        if (i == 0 && !sprite0Hit) {
                            sprite0evaluated = true;
                        }
                        if (numberOfSprites < 8)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                secondaryOam[j + (numberOfSprites * 4)] = oam[i + j];
                            }
                        }
                        numberOfSprites++;
                    }
                }
            }
        }

        private void SpriteRenderer()
        {
            if (spritesEnabled && currentScanline != 0 && currentScanline <= 239)
            {
                for (int i = 0; i < secondaryOam.Length; i += 4)
                {
                    int ypos = secondaryOam[i];
                    if (ypos == 0)
                    {
                        continue;
                    }

                    int palette = secondaryOam[i + 2] & 0x3;
                    bool behindBg = ((secondaryOam[i + 2] >> 5) & 0x1) != 0;
                    bool flipX = ((secondaryOam[i + 2] >> 6) & 0x1) != 0;
                    bool flipY = ((secondaryOam[i + 2] >> 7) & 0x1) != 0;
                    int xpos = secondaryOam[i + 3];
                    int row = flipY ? 7 - (currentScanline - (ypos + 1)) : currentScanline - (ypos + 1);
                    int tileAddress = ((secondaryOam[i + 1] * 0x10) + row) | spriteTableAdress;

                    byte tileData0 = _cart.PpuRead(tileAddress);
                    byte tileData1 = _cart.PpuRead(tileAddress + 8);
                    int bit0 = 0;
                    int bit1 = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (flipX)
                        {
                            bit0 = (tileData0 >> j) & 0x1;
                            bit1 = (tileData1 >> j) & 0x1;
                        }
                        else
                        {
                            bit0 = (tileData0 >> 7 - j) & 0x1;
                            bit1 = (tileData1 >> 7 - j) & 0x1;
                        }
                        int pixel = 0x10 | (palette << 2) | (bit1 << 1) | bit0;
                        if(xpos + j > (scanlinebuffer.Length - 1)) { break; }
                        if (behindBg && (scanlinebuffer[xpos + j] & 3) != 0) { continue; }
                        if ((pixel & 0x3) != 0 && (xpos + j) < (scanlinebuffer.Length - 1))
                        {
                            scanlinebuffer[xpos + j] = (byte)pixel;
                        }

                    }


                }
            }
        }


        private void FetchTimer(int currentDot)
        {
            if (currentScanline <= 239 || currentScanline == 261)
            {

                if (bgEnabled || spritesEnabled)
                {
                    if (currentDot != 0 && (currentDot % 8) == 0 && currentDot < 256 || (currentDot % 8) == 0 && currentDot >= 328)
                    {
                        FetchNewTile();
                    }
                    if (currentDot == 256)
                    {
                        IncrementY();
                    }
                    if (currentDot == 257)
                    {
                        ppuAddress &= ~0x1F;
                        ppuAddress &= ~0x400;
                        ppuAddress |= tempPpuAddress & 0x1F;
                        ppuAddress |= tempPpuAddress & 0x400;
                    }

                }

            }
        }

        private void BgRenderer(int currentDot)
        {
            if (currentDot < 256 && currentScanline <= 239 && bgEnabled)
            {
                byte pixel = 0;
                int pixelplace = (currentDot % 8) + fineX;
                pixel |= (byte)((tileData0 >> (15 - pixelplace)) & 0x1);
                pixel |= (byte)(((tileData1 >> (15 - pixelplace)) & 0x1) << 1);
                if(sprite0evaluated)
                {
                    int xpos = secondaryOam[3];
                    if (currentDot >= xpos && currentDot <= (xpos + 7))
                    {
                        int ypos = secondaryOam[0];
                        bool flipX = ((secondaryOam[2] >> 6) & 0x1) != 0;
                        bool flipY = ((secondaryOam[2] >> 7) & 0x1) != 0;
                        
                        int row = flipY ? 7 - (currentScanline - (ypos + 1)) : currentScanline - (ypos + 1);
                        int tileAddress = ((secondaryOam[1] * 0x10) + row) | spriteTableAdress;

                        byte tileData0 = _cart.PpuRead(tileAddress);
                        byte tileData1 = _cart.PpuRead(tileAddress + 8);
                        int bit0 = 0;
                        int bit1 = 0;
                        for (int j = 0; j < 8; j++)
                        {
                            if (flipX)
                            {
                                bit0 = (tileData0 >> j) & 0x1;
                                bit1 = (tileData1 >> j) & 0x1;
                            }
                            else
                            {
                                bit0 = (tileData0 >> 7 - j) & 0x1;
                                bit1 = (tileData1 >> 7 - j) & 0x1;
                            }
                            int sprite0pixel = (bit1 << 1) | bit0;

                            if ((pixel & 3) != 0 && sprite0pixel != 0)
                            {
                                sprite0Hit = true;
                                sprite0evaluated = false;
                            }
                        }
                    }
                    }
                if(pixelplace > 7)
                {
                    if (((ppuAddress + 1)  & 64) != 64)
                    {
                        if (((ppuAddress + 1) & 2) == 2)
                        {
                            pixel |= (byte)((bufferTileAttribute & 0x3) << 2);
                        }
                        else
                        {
                            pixel |= (byte)(((bufferTileAttribute >> 2) & 0x3) << 2);
                        }
                    }
                    else
                    {
                        if (((ppuAddress + 1) & 2) == 2)
                        {
                            pixel |= (byte)(((bufferTileAttribute >> 4) & 0x3) << 2);
                        }
                        else
                        {
                            pixel |= (byte)(((bufferTileAttribute >> 6) & 0x3) << 2);
                        }
                    }
                }
                else
                {
                    if ((ppuAddress & 64) != 64)
                    {
                        if ((ppuAddress & 2) == 2)
                        {
                            pixel |= (byte)((tileAttribute & 0x3) << 2);
                        }
                        else
                        {
                            pixel |= (byte)(((tileAttribute >> 2) & 0x3) << 2);
                        }
                    }
                    else
                    {
                        if ((ppuAddress & 2) == 2)
                        {
                            pixel |= (byte)(((tileAttribute >> 4) & 0x3) << 2);
                        }
                        else
                        {
                            pixel |= (byte)(((tileAttribute >> 6) & 0x3) << 2);
                        }
                    }
                }
                
                if ((pixel & 3) == 0) pixel = 0;
                scanlinebuffer[currentDot] = pixel;
            }
            if (currentDot < 256 && currentScanline <= 239 && !bgEnabled)
            {
                scanlinebuffer[currentDot] = 0;
            }
        }


        private void FetchNewTile()
        {
            int tileAddress = 0x2000 | (ppuAddress & 0x0FFF);
            int attributeAddress = 0x23C0 | (ppuAddress & 0x0C00) | ((ppuAddress >> 4) & 0x38) | ((ppuAddress >> 2) & 0x07);

            int row = (ppuAddress >> 12) & 0x7;
            tilePointer = ((ReadPpuMemory(tileAddress) * 0x10) + row) | backgroundTableAdress;

            tileAttribute = bufferTileAttribute;
            bufferTileAttribute = ReadPpuMemory(attributeAddress);
            tileData0 = (tileData0 & 0xFF) << 8;
            tileData1 = (tileData1 & 0xFF) << 8;
            tileData0 |= ReadPpuMemory(tilePointer);
            tileData1 |= ReadPpuMemory(tilePointer + 8);

            if ((ppuAddress & 0x001F) == 31)
            {
                // if coarse X == 31
                ppuAddress &= ~0x001F;      // coarse X = 0
                ppuAddress ^= 0x0400;         // switch horizontal nametable
            }
            else
            {
                ppuAddress += 1;              // increment coarse X
            }

     


        }
        private void IncrementY()
        {
            if ((ppuAddress & 0x7000) != 0x7000)
            {// if fine Y < 7
                ppuAddress += 0x1000;
            }// increment fine Y
            else
            {
                ppuAddress &= ~0x7000;                     // fine Y = 0
                int y = (ppuAddress & 0x03E0) >> 5;       // let y = coarse Y
                if (y == 29)
                {
                    y = 0;                         // coarse Y = 0
                    ppuAddress ^= 0x0800;           // switch vertical nametable
                }
                else if (y == 31)
                {
                    y = 0; // coarse Y = 0, nametable not switched
                }
                else
                {
                    y += 1;  // increment coarse Y
                }
                ppuAddress = (ppuAddress & ~0x03E0) | (y << 5);     // put coarse Y back into v
            }
        }
        public void WritePpuRegister(int address, byte data)
        {
            lastWritten = data;
            if (address == 0x4014)
            {
                int tempAdress = data << 8;
                for (int i = 0; i < oam.Length; i++)
                {
                    oam[i] = _core.ReadMemory(tempAdress++);
                    //should stall cpu for 514 cycles
                }
                return;
            }
            switch(address & 0x7)
            {
                case 0: WritePpuCtrl(data); break;
                case 1: WritePpuMask(data); break;
                case 3: oamAddr = data; break;
                case 4: oam[oamAddr++] = data; break;
                case 5:
                    {
                        if (!addressLatch)
                        {
                            xScroll = data;
                            fineX = data & 0x7;
                            tempPpuAddress &= ~0x1F;
                            tempPpuAddress |= data >> 3;
                        }                     
                        else
                        {
                            yScroll = data;
                            tempPpuAddress &= 0x8C1F;
                            tempPpuAddress |= (data & 0x7) << 12;
                            tempPpuAddress |= ((data >> 3) & 0x7) << 5;
                            tempPpuAddress |= ((data >> 6) & 0x3) << 8;
                        }
                        addressLatch = !addressLatch;
                        break;
                    }
                case 6:
                    {
                        if (!addressLatch)
                        {
                            tempPpuAddress &= ~0xFF00;
                            tempPpuAddress = (data & 0x3F) << 8;
                        }
                            
                        else
                        {
                            tempPpuAddress &= ~0xFF;
                            tempPpuAddress |= data;
                            tempPpuAddress &= 0x7FFF;
                            ppuAddress = tempPpuAddress;
                        }
                           
                        ppuAddress &= 0x3FFF;
                        addressLatch = !addressLatch;
                        break;
                    }
                case 7:
                    {
                        WritePpuMemory(ppuAddress, data);
                        ppuAddress += vramAddressIncrement;
                        break;
                    }
            }

        }

        public void WritePpuMemory(int address, byte data)
        {
            if(address < 0x2000)
            {
                _cart.PpuWrite(address, data);
            }
            else if(address < 0x3F00)
            {
                vram[address & 0x7FF] = data;
            }
            else
            {
                if((address & 0x10) !=0 && (address & 3) == 0)
                {
                    address &= ~0x10;
                }
                paletteRam[address & 0x1F] = data;
            }
        }

        public byte ReadPpuMemory(int address)
        {

            if (address < 0x2000)
            {
                return _cart.PpuRead(address);
            }
            else if (address < 0x3F00)
            {
                return vram[address & 0x7FF];
            }
            else
            {
                return paletteRam[address & 0x1F];
            }
        }

    

        public byte ReadPpuRegister(int address)
        {
            if (address == 0x4014)
            {
                //
            }
            switch (address & 0x7)
            {
                case 2: return ReadPpuStatus();
                case 4: return oam[oamAddr];
                case 7:
                    {
                        byte value = readBuffer;
                        readBuffer = ReadPpuMemory(ppuAddress);
                        ppuAddress += vramAddressIncrement;
                        return value;
                    }

            }
            return 0;
        }

        private byte ReadPpuStatus()
        {
            addressLatch = false;
            int value = lastWritten & 0x1F;
            value |= spriteOverflow ? 1 << 5 : 0;
            value |= sprite0Hit ? 1 << 6 : 0;
            value |= inVblank ? 1 << 7 : 0;
            inVblank = false;
            return (byte)value;

        }

        private void WritePpuMask(byte data)
        {
            ppuMask = data;
            grayScale = (data & 1) != 0;
            showLeftBg = ((data >> 1) & 1) != 0;
            showLeftSprite = ((data >> 2) & 1) != 0;
            bgEnabled = ((data >> 3) & 1) != 0;
            spritesEnabled = ((data >> 4) & 1) != 0;
            //emphasis missing, last 3 bits
        }

        private void WritePpuCtrl(byte data)
        {
            ppuCtrl = data;
            tempPpuAddress &= ~0xC00;
            tempPpuAddress |= (data & 3) << 10;
            vramAddressIncrement = ((data >> 2) & 1) != 0 ? 0x20 : 0x1;
            spriteTableAdress = ((data >> 3) & 1) != 0 ? 0x1000 : 0x0000;
            backgroundTableAdress = ((data >> 4) & 1) != 0 ? 0x1000 : 0x0000;
            largeSprites = ((data >> 5) & 1) != 0;
            vblank_NMI = ((data >> 7) & 1) != 0;
        }

        public bool CheckNMI()
        {
            if(inVblank && vblank_NMI)
            {
                vblank_NMI = false;
                return true;
            }
            return false;
        }
    }
}

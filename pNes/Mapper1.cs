﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper1
    {
        private byte[] prgRom;
        private byte[] chrRom;
        private byte[] prgRam = new byte[prgRamBankSize];
        private byte[] sRam = new byte[chrRomBankSize8k];
        private byte[,] ppuRam = new byte[4, ppuRamBankSize];

        private const int prgRomBankSize32k = 0x8000;
        private const int prgRomBankSize16k = 0x4000;
        private const int chrRomBankSize8k = 0x2000;
        private const int prgRomBankSize8k = 0x2000;
        private const int chrRomBankSize4k = 0x1000;
        private const int prgRamBankSize = 0x2000;
        private const int ppuRamBankSize = 0x400;

        //Load register ($8000-$FFFF)
        private byte loadRegister;
        private byte shiftCount;
        //Control (internal, $8000-$9FFF)
        private enum Mirroring
        { OneScreenLower, OneScreenUpper, Vertical, Horizontal };
        private byte currentMirroring;
        private byte prgRomBankMode = 3; // cart set ctrl reg to 0x0C at startup, resulting in bankmode 3.
        private byte chrRomBankMode = 0;
        //CHR bank 0 (internal, $A000-$BFFF)
        private byte chrBank0;
        //CHR bank 1 (internal, $C000-$DFFF)
        private byte chrBank1;
        //PRG bank (internal, $E000-$FFFF)
        private byte prgBank;
        private bool prgRamEnabled;


        private int cyclesBetweenWrites;



        public Mapper1(byte[] prgrom, byte[] chrrom)
        {
            prgRom = new byte[prgrom.Length];
            chrRom = new byte[chrrom.Length];
            Array.Copy(prgrom, prgRom, prgRom.Length);
            Array.Copy(chrrom, chrRom, chrRom.Length);
        }

        public void Tick()
        {
            if (cyclesBetweenWrites > 0) cyclesBetweenWrites--;
        }

        private void WriteReg(int address, byte data)
        {
            if (cyclesBetweenWrites > 0) return;
            cyclesBetweenWrites = 3;
            if ((data & 0x80) != 0)
            {
                loadRegister = 0;
                shiftCount = 0;
                prgRomBankMode = 0x3;
                return;
            }
            loadRegister |= (byte)((data & 1) << shiftCount++);
            if (shiftCount < 5) return;
           
            switch((address >> 13) & 3)
            {
                case 0:
                    currentMirroring = (byte)(loadRegister & 0x3);
                    prgRomBankMode = (byte)((loadRegister >> 2) & 0x3);
                    chrRomBankMode = (byte)((loadRegister >> 4) & 0x1);
                    break;
                case 1:
                    chrBank0 = loadRegister;
                    if(chrRomBankMode == 0)
                    {
                        chrBank0 >>= 1;
                        chrBank0 &= 0x7;
                        if (((loadRegister >> 4) & 1) == 0) { prgRamEnabled = true; }
                        else prgRamEnabled = false;
                    }
                    break;
                case 2:
                    chrBank1 = loadRegister;
                    if (chrRomBankMode == 0)
                    {
                        if (((loadRegister >> 4) & 1) == 0) { prgRamEnabled = true; }
                        else prgRamEnabled = false;
                    }
                    break;
                case 3:
                    if(prgRomBankMode == 0 || prgRomBankMode == 1)
                    {
                        prgBank = (byte)((loadRegister & 0xF) >> 1);
                    }
                    else
                    {
                        prgBank = (byte)(loadRegister & 0xF);
                    } 
                    //prgRamEnabled = ((loadRegister >> 4) & 0x1) != 0;
                    break;
            }
            loadRegister = 0;
            shiftCount = 0;

        }

        public void WriteCart(int address, byte data)
        {
            if(address < 0x2000)
            {
                chrRom[address & (chrRom.Length - 1)] = data;
            }
            else if (address < 0x3F00)
            {
                WriteVram(address, data);
            }
            else if(address < 0x8000 && prgRamEnabled)
            {
                prgRam[address & prgRamBankSize - 1] = data;
            }
            else
            {
                WriteReg(address, data);
            }
        }
        public byte ReadCart(int address)
        {
            if(address < 0x2000) //ChrRom
            {
                return ReadCHR(address);
            }
            else if (address < 0x3F00)
            {
                return ReadVram(address);
            }
            else if (address < 0x8000 && prgRamEnabled)
            {
                return prgRam[address & prgRamBankSize - 1];
            }
            else
            {
                return ReadPRG(address);
            }

        }

        private byte ReadPRG(int address)
        {
            //PRG ROM bank mode (0, 1: switch 32 KB at $8000, ignoring low bit of bank number;
            //                    | 2: fix first bank at $8000 and switch 16 KB bank at $C000;
            //                    | 3: fix last bank at $C000 and switch 16 KB bank at $8000)
            switch (prgRomBankMode)
            {
                case 0:
                case 1: return prgRom[(prgRomBankSize32k * prgBank) + (address & (prgRomBankSize32k - 1))];
                case 2:
                    if (address < 0xC000)
                    {
                        return prgRom[address & (prgRomBankSize16k - 1)];
                    }
                    else
                    {
                        return prgRom[(prgRomBankSize16k * prgBank) + (address & (prgRomBankSize16k - 1))];
                    }
                case 3:
                    if (address < 0xC000)
                    {
                        return prgRom[(prgRomBankSize16k * prgBank) + (address & (prgRomBankSize16k - 1))];
                    }
                    else
                    {
                        return prgRom[(prgRom.Length - prgRomBankSize16k) + (address & (prgRomBankSize16k - 1))];
                    }
                default:return 0;


            }
        }

        private byte ReadCHR(int address)
        {
            // CHR ROM bank mode (0: switch 8 KB at a time; 1: switch two separate 4 KB banks)
            if(chrRomBankMode != 0)
            {
                if(address < 0x1000)
                {
                    return chrRom[(chrBank0 * chrRomBankSize4k) + (address & (chrRomBankSize4k - 1))];
                }
                else
                {
                    return chrRom[(chrBank1 * chrRomBankSize4k) + (address & (chrRomBankSize4k - 1))];
                }
            }
            else
            {
                return chrRom[(chrBank0 * chrRomBankSize8k) + (address & (chrRomBankSize8k - 1))];
            }
        }

        private byte ReadVram(int address)
        {
            switch(currentMirroring)
            {
                case 0:
                    switch ((address >> 10) & 3)
                    {
                        case 0: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 1: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 2: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 3: return ppuRam[0, address & ppuRamBankSize - 1];
                    }
                    break;
                case 1:
                    switch ((address >> 10) & 3)
                    {
                        case 0: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 1: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 2: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 3: return ppuRam[1, address & ppuRamBankSize - 1];
                    }
                    break;
                case 2:
                    switch ((address >> 10) & 3)
                    {
                        case 0: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 1: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 2: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 3: return ppuRam[1, address & ppuRamBankSize - 1];
                    }
                    break;
                case 3:
                    switch ((address >> 10) & 3)
                    {
                        case 0: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 1: return ppuRam[0, address & ppuRamBankSize - 1];
                        case 2: return ppuRam[1, address & ppuRamBankSize - 1];
                        case 3: return ppuRam[1, address & ppuRamBankSize - 1];
                    }
                    break;
            }
            return 0;
          
        }

        private void WriteVram(int address, byte data)
        {
            switch (currentMirroring)
            {
                case 0:
                    switch ((address >> 10) & 3)
                    {
                        case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 1: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 2: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 3: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    }
                    break;
                case 1:
                    switch ((address >> 10) & 3)
                    {
                        case 0: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 1: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 2: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    }
                    break;
                case 2:
                    switch ((address >> 10) & 3)
                    {
                        case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 1: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 2: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    }
                    break;
                case 3:
                    switch ((address >> 10) & 3)
                    {
                        case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 1: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                        case 2: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                        case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    }
                    break;
            }
        }
    }
}

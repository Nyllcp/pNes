using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    abstract class Mapper
    {

        protected Rom _rom;

        protected byte[] prgRam = new byte[prgRamBankSize];
        protected byte[,] ppuRam = new byte[4, ppuRamBankSize];
        protected bool iflag = false;
        protected bool verticalMirroring = false;

        protected int prg32kBankCount = 0;
        protected int prg16kBankCount = 0;
        protected int prg8kBankCount = 0;

        protected int chr8kBankCount = 0;
        protected int chr4kBankCount = 0;
        protected int chr2kBankCount = 0;
        protected int chr1kBankCount = 0;



        protected const int prgRomBankSize32k = 0x8000;
        protected const int prgRomBankSize16k = 0x4000;
        protected const int prgRomBankSize8k = 0x2000;

        protected const int chrRomBankSize8k = 0x2000;
        protected const int chrRomBankSize4k = 0x1000;
        protected const int chrRomBankSize2k = 0x800;
        protected const int chrRomBankSize1k = 0x400;

        protected const int prgRamBankSize = 0x2000;
        protected const int ppuRamBankSize = 0x400;

        public bool Iflag { get { bool value = iflag; iflag = false; return value; } }

        public virtual void Init(Rom rom)
        {
            _rom = rom;
            prg16kBankCount = _rom.prgRomCount;
            prg32kBankCount = prg16kBankCount >> 1;
            prg8kBankCount = prg16kBankCount << 1;

            chr8kBankCount = _rom.chrRomCount;
            chr4kBankCount = chr8kBankCount << 1;
            chr2kBankCount = chr4kBankCount << 1;
            chr1kBankCount = chr2kBankCount << 1;
            verticalMirroring = _rom.verticalMirroring;
        }

        public virtual void Tick()
        {

        }


        public virtual void WriteCart(int address, byte data)
        {
            if (address < 0x2000)
            {
                WriteCHR(address, data);
            }
            else if (address < 0x3F00)
            {
                WriteVram(address, data);
            }
            else if (address < 0x8000)
            {
                prgRam[address & prgRamBankSize - 1] = data;
            }
            else
            {
              
            }
        }
        public virtual byte ReadCart(int address)
        {
            if (address < 0x2000) //ChrRom
            {
                return ReadCHR(address);
            }
            else if (address < 0x3F00)
            {
                return ReadVram(address);
            }
            else if (address < 0x8000)
            {
                return prgRam[address & prgRamBankSize - 1];
            }
            else
            {
                return ReadPRG(address);
            }

        }

        protected virtual byte ReadPRG(int address)
        {
            return _rom.prgRom[address & (_rom.prgRom.Length - 1)];
        }

        protected virtual byte ReadCHR(int address)
        {
            return _rom.chrRom[address & (_rom.chrRom.Length - 1)];       
        }

        protected virtual void WriteCHR(int address, byte data)
        {
            if (_rom.chrRamEnabled)
            {
                _rom.chrRom[address & (_rom.chrRom.Length - 1)] = data;
            }
        }

        protected virtual byte ReadVram(int address)
        {
            if(verticalMirroring)
            {
                switch ((address >> 10) & 3)
                {
                    case 0: return ppuRam[0, address & ppuRamBankSize - 1];
                    case 1: return ppuRam[1, address & ppuRamBankSize - 1];
                    case 2: return ppuRam[0, address & ppuRamBankSize - 1];
                    case 3: return ppuRam[1, address & ppuRamBankSize - 1];
                    default: return 0;
                }
            }
            else
            {
                switch ((address >> 10) & 3)
                {
                    case 0: return ppuRam[0, address & ppuRamBankSize - 1];
                    case 1: return ppuRam[0, address & ppuRamBankSize - 1];
                    case 2: return ppuRam[1, address & ppuRamBankSize - 1];
                    case 3: return ppuRam[1, address & ppuRamBankSize - 1];
                    default: return 0;
                }
            }

        }

        protected virtual void WriteVram(int address, byte data)
        {
            if (verticalMirroring)
            {
                switch ((address >> 10) & 3)
                {
                    case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 1: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    case 2: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                }
            }
            else
            {
                switch ((address >> 10) & 3)
                {
                    case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 1: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 2: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                }
            }
           
        }

    }
}

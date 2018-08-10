using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Mapper7:Mapper
    {

        private int prgBankNo;
        private int nameTable = 0;


        protected override void WritePRG(int address, byte data)
        {
            prgBankNo = (data & 0x7) % prg32kBankCount;
            nameTable = (data >> 4) & 1;
        }

        protected override byte ReadPRG(int address)
        {
            address &= prgRomBankSize32k - 1;
            return _rom.prgRom[address + (prgBankNo * prgRomBankSize32k)];
        }

        protected override byte ReadVram(int address)
        {
            if (nameTable != 0)
            {
                switch ((address >> 10) & 3)
                {
                    case 0: return ppuRam[1, address & ppuRamBankSize - 1];
                    case 1: return ppuRam[1, address & ppuRamBankSize - 1];
                    case 2: return ppuRam[1, address & ppuRamBankSize - 1];
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
                    case 2: return ppuRam[0, address & ppuRamBankSize - 1];
                    case 3: return ppuRam[0, address & ppuRamBankSize - 1];
                    default: return 0;
                }
            }

        }

        protected override void WriteVram(int address, byte data)
        {
            if (nameTable != 0)
            {
                switch ((address >> 10) & 3)
                {
                    case 0: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    case 1: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    case 2: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                    case 3: ppuRam[1, address & ppuRamBankSize - 1] = data; break;
                }
            }
            else
            {
                switch ((address >> 10) & 3)
                {
                    case 0: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 1: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 2: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                    case 3: ppuRam[0, address & ppuRamBankSize - 1] = data; break;
                }
            }

        }
        public override void WriteSaveState(ref Savestate state)
        {
            state.nameTable = nameTable;
            state.prgBankNo = prgBankNo;
            base.WriteSaveState(ref state);
        }
        public override void LoadSaveState(ref Savestate state)
        {
            nameTable = state.nameTable;
            prgBankNo = state.prgBankNo;
            base.LoadSaveState(ref state);
        }
    }
}

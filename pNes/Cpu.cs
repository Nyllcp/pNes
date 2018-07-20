﻿using System;
using System.ComponentModel;
using System.Globalization;

namespace pNes
{
    /// <summary>
    /// An Implementation of a 6502 Processor
    /// </summary>
    class Cpu
    {
        #region Fields
        private int _programCounter;
        private int _stackPointer;
        private Core _core;
        #endregion

        //All of the properties here are public and read only to facilitate ease of debugging and testing.
        #region Properties
        /// <summary>
        /// The Accumulator. This value is implemented as an integer intead of a byte.
        /// This is done so we can detect wrapping of the value and set the correct number of cycles.
        /// </summary>
        public int Accumulator { get; private set; }
        /// <summary>
        /// The X Index Register
        /// </summary>
        public int XRegister { get; private set; }
        /// <summary>
        /// The Y Index Register
        /// </summary>
        public int YRegister { get; private set; }
        /// <summary>
        /// The Current Op Code being executed by the system
        /// </summary>
        public int CurrentOpCode { get; private set; }

        /// <summary>
        /// Points to the Current Address of the instruction being executed by the system. 
        /// The PC wraps when the value is greater than 65535, or less than 0. 
        /// </summary>
        public int ProgramCounter
        {
            get { return _programCounter; }
            private set { _programCounter = WrapProgramCounter(value); }
        }
        /// <summary>
        /// Points to the Current Position of the Stack.
        /// This value is a 00-FF value but is offset to point to the location in memory where the stack resides.
        /// </summary>
        public int StackPointer
        {
            get { return _stackPointer; }
            private set
            {
                if (value > 0xFF)
                    _stackPointer = value - 0x100;
                else if (value < 0x00)
                    _stackPointer = value + 0x100;
                else
                    _stackPointer = value;
            }
        }
        /// <summary>
        /// The number of cycles before the next interrupt.
        /// </summary>
        public int InterruptPeriod { get; private set; }
        /// <summary>
        /// The number of cycles left before the next interrupt.
        /// </summary>
        public int NumberofCyclesLeft { get; private set; }
        /// <summary>
        /// The Memory
        /// </summary>
        //Status Registers
        /// <summary>
        /// This is the carry flag. when adding, if the result is greater than 255 or 99 in BCD Mode, then this bit is enabled. 
        /// In subtraction this is reversed and set to false if a borrow is required IE the result is less than 0
        /// </summary>
        public bool CarryFlag { get; private set; }
        /// <summary>
        /// Is true if one of the registers is set to zero.
        /// </summary>
        public bool ZeroFlag { get; private set; }
        /// <summary>
        /// This determines if Interrupts are currently disabled.
        /// This flag is turned on during a reset to prevent an interrupt from occuring during startup/Initialization.
        /// If this flag is true, then the IRQ pin is ignored.
        /// </summary>
        public bool DisableInterruptFlag { get; private set; }
        /// <summary>
        /// Binary Coded Decimal Mode is set/cleared via this flag.
        /// when this mode is in effect, a byte represents a number from 0-99. 
        /// </summary>
        public bool DecimalFlag { get; private set; }
        /// <summary>
        /// This property is set when an overflow occurs. An overflow happens if the high bit(7) changes during the operation. Remember that values from 128-256 are negative values
        /// as the high bit is set to 1.
        /// Examples:
        /// 64 + 64 = -128 
        /// -128 + -128 = 0
        /// </summary>
        public bool OverflowFlag { get; private set; }
        /// <summary>
        /// Set to true if the result of an operation is negative in ADC and SBC operations. 
        /// Remember that 128-256 represent negative numbers when doing signed math.
        /// In shift operations the sign holds the carry.
        /// </summary>
        public bool NegativeFlag { get; private set; }
        #endregion

        #region Public Methods
        /// <summary>
        /// Default Constructor, Instantiates a new instance of the processor.
        /// </summary>
        /// 
        public Cpu(Core core)
        {
            _core = core;
        }

        /// <summary>
        /// Initializes the processor to its default state.
        /// </summary>
        public void Reset()
        {
            StackPointer = 0x1FD;

            //Set the Program Counter to the Reset Vector Address.
            ProgramCounter = 0xFFFC;
            //Reset the Program Counter to the Address contained in the Reset Vector
            ProgramCounter = GetAddressByAddressingMode(AddressingMode.Absolute);

            InterruptPeriod = 20;
            NumberofCyclesLeft = InterruptPeriod;

            CurrentOpCode = _core.ReadMemory(ProgramCounter);

//#if DEBUG
//            SetDisassembly();
//#endif

            DisableInterruptFlag = true;
        }

        /// <summary>
        /// Performs the next step on the processor
        /// </summary>
        public void NextStep()
        {
            ProgramCounter++;
            ExecuteOpCode();

            //We want to add here instead of replace because the number of cycles left could be zero.
            if (NumberofCyclesLeft < 0)
                NumberofCyclesLeft += InterruptPeriod;

            //Grabbing this at the end, ensure thats when we read the CurrentOp Code field, that we have the correct OpCode for the instruction we are going to execute Next.
            CurrentOpCode = _core.ReadMemory(ProgramCounter);

//#if DEBUG
//            SetDisassembly();
//#endif

        }

        /// <summary>
        /// Loads a program into the processors memory
        /// </summary>
        /// <param name="offset">The offset in memory when loading the program.</param>
        /// <param name="program">The program to be loaded</param>
        /// <param name="initialProgramCounter">The initial PC value, this is the entry point of the program</param>


        /// <summary>
        /// The InterruptRequest or IRQ
        /// </summary>
        public void InterruptRequest()
        {
            if (DisableInterruptFlag)
                return;

            ProgramCounter--;
            BreakOperation(false, 0xFFFE);
            CurrentOpCode = _core.ReadMemory(ProgramCounter);
//#if DEBUG
//            SetDisassembly();
//#endif
        }

        /// <summary>
        /// The InterruptRequest or IRQ
        /// </summary>
        public void NonMaskableInterrupt()
        {
            ProgramCounter--;
            BreakOperation(false, 0xFFFA);
            CurrentOpCode = _core.ReadMemory(ProgramCounter);
//#if DEBUG
//            SetDisassembly();
//#endif
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Executes an Opcode
        /// </summary>
        private void ExecuteOpCode()
        {
            //The x+ cycles denotes that if a page wrap occurs, then an additional cycle is consumed.
            //The x++ cycles denotes that 1 cycle is added when a branch occurs and it on the same page, and two cycles are added if its on a different page./
            //This is handled inside the GetValueFromMemory Method
            switch (CurrentOpCode)
            {
                #region Add / Subtract Operations
                //STA Store Accumulator In Memory, Immediate, 2 Bytes, 2 Cycles
                case 0x69:
                    {
                        AddWithCarryOperation(AddressingMode.Immediate);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                //STA Store Accumulator In Memory, Zero Page, 2 Bytes, 3 Cycles
                case 0x65:
                    {
                        AddWithCarryOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STA Store Accumulator In Memory, Zero Page X, 2 Bytes, 4 Cycles
                case 0x75:
                    {
                        AddWithCarryOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STA Store Accumulator In Memory, Absolute, 3 Bytes, 4 Cycles
                case 0x6D:
                    {
                        AddWithCarryOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STA Store Accumulator In Memory, Absolute X, 3 Bytes, 4+ Cycles
                case 0x7D:
                    {
                        AddWithCarryOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STA Store Accumulator In Memory, Absolute Y, 3 Bytes, 4+ Cycles
                case 0x79:
                    {
                        AddWithCarryOperation(AddressingMode.AbsoluteY);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STA Store Accumulator In Memory, Indexed Indirect, 2 Bytes, 6 Cycles
                case 0x61:
                    {
                        AddWithCarryOperation(AddressingMode.IndirectX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STA Store Accumulator In Memory, Indexed Indirect, 2 Bytes, 5+ Cycles
                case 0x71:
                    {
                        AddWithCarryOperation(AddressingMode.IndirectY);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //SBC Subtract with Borrow, Immediate, 2 Bytes, 2 Cycles
                case 0xE9:
                    {
                        SubtractWithBorrowOperation(AddressingMode.Immediate);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                //SBC Subtract with Borrow, Zero Page, 2 Bytes, 3 Cycles
                case 0xE5:
                    {
                        SubtractWithBorrowOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //SBC Subtract with Borrow, Zero Page X, 2 Bytes, 4 Cycles
                case 0xF5:
                    {
                        SubtractWithBorrowOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //SBC Subtract with Borrow, Absolute, 3 Bytes, 4 Cycles
                case 0xED:
                    {
                        SubtractWithBorrowOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //SBC Subtract with Borrow, Absolute X, 3 Bytes, 4+ Cycles
                case 0xFD:
                    {
                        SubtractWithBorrowOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //SBC Subtract with Borrow, Absolute Y, 3 Bytes, 4+ Cycles
                case 0xF9:
                    {
                        SubtractWithBorrowOperation(AddressingMode.AbsoluteY);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //SBC Subtract with Borrow, Indexed Indirect, 2 Bytes, 6 Cycles
                case 0xE1:
                    {
                        SubtractWithBorrowOperation(AddressingMode.IndirectX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //SBC Subtract with Borrow, Indexed Indirect, 2 Bytes, 5+ Cycles
                case 0xF1:
                    {
                        SubtractWithBorrowOperation(AddressingMode.IndirectY);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                #endregion

                #region Branch Operations
                //BCC Branch if Carry is Clear, Relative, 2 Bytes, 2++ Cycles
                case 0x90:
                    {

                        BranchOperation(!CarryFlag);
                        NumberofCyclesLeft -= 2;
                        break;

                    }
                //BCS Branch if Carry is Set, Relative, 2 Bytes, 2++ Cycles
                case 0xB0:
                    {
                        BranchOperation(CarryFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                //BEQ Branch if Zero is Set, Relative, 2 Bytes, 2++ Cycles
                case 0xF0:
                    {
                        BranchOperation(ZeroFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }

                // BMI Branch if Negative Set
                case 0x30:
                    {
                        BranchOperation(NegativeFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                //BNE Branch if Zero is Not Set, Relative, 2 Bytes, 2++ Cycles
                case 0xD0:
                    {
                        BranchOperation(!ZeroFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                // BPL Branch if Negative Clear, 2 Bytes, 2++ Cycles
                case 0x10:
                    {
                        BranchOperation(!NegativeFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                // BVC Branch if Overflow Clear, 2 Bytes, 2++ Cycles
                case 0x50:
                    {
                        BranchOperation(!OverflowFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                // BVS Branch if Overflow Set, 2 Bytes, 2++ Cycles
                case 0x70:
                    {
                        BranchOperation(OverflowFlag);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                #endregion

                #region BitWise Comparison Operations
                //AND Compare Memory with Accumulator, Immediate, 2 Bytes, 2 Cycles
                case 0x29:
                    {
                        AndOperation(AddressingMode.Immediate);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //AND Compare Memory with Accumulator, Zero Page, 2 Bytes, 2 Cycles
                case 0x25:
                    {
                        AndOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //AND Compare Memory with Accumulator, Zero PageX, 2 Bytes, 3 Cycles
                case 0x35:
                    {
                        AndOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //AND Compare Memory with Accumulator, Absolute,  3 Bytes, 4 Cycles
                case 0x2D:
                    {
                        AndOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //AND Compare Memory with Accumulator, AbsolueteX 3 Bytes, 4+ Cycles
                case 0x3D:
                    {
                        AndOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //AND Compare Memory with Accumulator, AbsoluteY, 3 Bytes, 4+ Cycles
                case 0x39:
                    {
                        AndOperation(AddressingMode.AbsoluteY);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //AND Compare Memory with Accumulator, IndexedIndirect, 2 Bytes, 6 Cycles
                case 0x21:
                    {
                        AndOperation(AddressingMode.IndirectX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //AND Compare Memory with Accumulator, IndirectIndexed, 2 Bytes, 5 Cycles
                case 0x31:
                    {
                        AndOperation(AddressingMode.IndirectY);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //BIT Compare Memory with Accumulator, Zero Page, 2 Bytes, 3 Cycles
                case 0x24:
                    {
                        BitOperation(AddressingMode.ZeroPage);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 3;
                        break;
                    }
                //BIT Compare Memory with Accumulator, Absolute, 2 Bytes, 4 Cycles
                case 0x2C:
                    {
                        BitOperation(AddressingMode.Absolute);
                        IncrementProgramCounter(3);
                        NumberofCyclesLeft -= 4;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, Immediate, 2 Bytes, 2 Cycles
                case 0x49:
                    {
                        EorOperation(AddressingMode.Immediate);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 2;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, Zero Page, 2 Bytes, 3 Cycles
                case 0x45:
                    {
                        EorOperation(AddressingMode.ZeroPage);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 3;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, Zero Page X, 2 Bytes, 4 Cycles
                case 0x55:
                    {
                        EorOperation(AddressingMode.ZeroPageX);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 4;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, Absolute, 3 Bytes, 4 Cycles
                case 0x4D:
                    {
                        EorOperation(AddressingMode.Absolute);
                        IncrementProgramCounter(3);
                        NumberofCyclesLeft -= 4;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, Absolute X, 3 Bytes, 4+ Cycles
                case 0x5D:
                    {
                        EorOperation(AddressingMode.AbsoluteX);
                        IncrementProgramCounter(3);
                        NumberofCyclesLeft -= 4;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, Absolute Y, 3 Bytes, 4+ Cycles
                case 0x59:
                    {
                        EorOperation(AddressingMode.AbsoluteY);
                        IncrementProgramCounter(3);
                        NumberofCyclesLeft -= 4;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, IndexedIndirect, 2 Bytes 6 Cycles
                case 0x41:
                    {
                        EorOperation(AddressingMode.IndirectX);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 6;
                        break;
                    }
                //EOR Exclusive OR Memory with Accumulator, IndirectIndexed, 2 Bytes 5 Cycles
                case 0x51:
                    {
                        EorOperation(AddressingMode.IndirectY);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 5;
                        break;
                    }
                //ORA Compare Memory with Accumulator, Immediate, 2 Bytes, 2 Cycles
                case 0x09:
                    {
                        OrOperation(AddressingMode.Immediate);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ORA Compare Memory with Accumulator, Zero Page, 2 Bytes, 2 Cycles
                case 0x05:
                    {
                        OrOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ORA Compare Memory with Accumulator, Zero PageX, 2 Bytes, 3 Cycles
                case 0x15:
                    {
                        OrOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ORA Compare Memory with Accumulator, Absolute,  3 Bytes, 4 Cycles
                case 0x0D:
                    {
                        OrOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ORA Compare Memory with Accumulator, AbsolueteX 3 Bytes, 4+ Cycles
                case 0x1D:
                    {
                        OrOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ORA Compare Memory with Accumulator, AbsoluteY, 3 Bytes, 4+ Cycles
                case 0x19:
                    {
                        OrOperation(AddressingMode.AbsoluteY);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ORA Compare Memory with Accumulator, IndexedIndirect, 2 Bytes, 6 Cycles
                case 0x01:
                    {
                        OrOperation(AddressingMode.IndirectX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ORA Compare Memory with Accumulator, IndirectIndexed, 2 Bytes, 5 Cycles
                case 0x11:
                    {
                        OrOperation(AddressingMode.IndirectY);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                #endregion

                #region Clear Flag Operations
                //CLC Clear Carry Flag, Implied, 1 Byte, 2 Cycles
                case 0x18:
                    {
                        CarryFlag = false;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //CLD Clear Decimal Flag, Implied, 1 Byte, 2 Cycles
                case 0xD8:
                    {
                        DecimalFlag = false;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;

                    }
                //CLI Clear Interrupt Flag, Implied, 1 Byte, 2 Cycles
                case 0x58:
                    {
                        DisableInterruptFlag = false;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;

                    }
                //CLV Clear Overflow Flag, Implied, 1 Byte, 2 Cycles
                case 0xB8:
                    {
                        OverflowFlag = false;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }

                #endregion

                #region Compare Operations
                //CMP Compare Accumulator with Memory, Immediate, 2 Bytes, 2 Cycles
                case 0xC9:
                    {
                        CompareOperation(AddressingMode.Immediate, Accumulator);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Zero Page, 2 Bytes, 3 Cycles
                case 0xC5:
                    {
                        CompareOperation(AddressingMode.ZeroPage, Accumulator);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Zero Page x, 2 Bytes, 4 Cycles
                case 0xD5:
                    {
                        CompareOperation(AddressingMode.ZeroPageX, Accumulator);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Absolute, 3 Bytes, 4 Cycles
                case 0xCD:
                    {
                        CompareOperation(AddressingMode.Absolute, Accumulator);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Absolute X, 2 Bytes, 4 Cycles
                case 0xDD:
                    {
                        CompareOperation(AddressingMode.AbsoluteX, Accumulator);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Absolute Y, 2 Bytes, 4 Cycles
                case 0xD9:
                    {
                        CompareOperation(AddressingMode.AbsoluteY, Accumulator);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Indirect X, 2 Bytes, 6 Cycles
                case 0xC1:
                    {
                        CompareOperation(AddressingMode.IndirectX, Accumulator);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CMP Compare Accumulator with Memory, Indirect Y, 2 Bytes, 5 Cycles
                case 0xD1:
                    {
                        CompareOperation(AddressingMode.IndirectY, Accumulator);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CPX Compare Accumulator with X Register, Immediate, 2 Bytes, 2 Cycles
                case 0xE0:
                    {
                        CompareOperation(AddressingMode.Immediate, XRegister);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CPX Compare Accumulator with X Register, Zero Page, 2 Bytes, 3 Cycles
                case 0xE4:
                    {
                        CompareOperation(AddressingMode.ZeroPage, XRegister);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CPX Compare Accumulator with X Register, Absolute, 3 Bytes, 4 Cycles
                case 0xEC:
                    {
                        CompareOperation(AddressingMode.Absolute, XRegister);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //CPY Compare Accumulator with Y Register, Immediate, 2 Bytes, 2 Cycles
                case 0xC0:
                    {
                        CompareOperation(AddressingMode.Immediate, YRegister);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CPY Compare Accumulator with Y Register, Zero Page, 2 Bytes, 3 Cycles
                case 0xC4:
                    {
                        CompareOperation(AddressingMode.ZeroPage, YRegister);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //CPY Compare Accumulator with Y Register, Absolute, 3 Bytes, 4 Cycles
                case 0xCC:
                    {
                        CompareOperation(AddressingMode.Absolute, YRegister);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                #endregion

                #region Increment/Decrement Operations
                //DEC Decrement Memory by One, Zero Page, 2 Bytes, 5 Cycles
                case 0xC6:
                    {
                        ChangeMemoryByOne(AddressingMode.ZeroPage, true);

                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //DEC Decrement Memory by One, Zero Page X, 2 Bytes, 6 Cycles
                case 0xD6:
                    {
                        ChangeMemoryByOne(AddressingMode.ZeroPageX, true);

                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //DEC Decrement Memory by One, Absolute, 3 Bytes, 6 Cycles
                case 0xCE:
                    {
                        ChangeMemoryByOne(AddressingMode.Absolute, true);

                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(3);
                        break;
                    }
                //DEC Decrement Memory by One, Implied, 3 Bytes, 7 Cycles
                case 0xDE:
                    {
                        ChangeMemoryByOne(AddressingMode.AbsoluteX, true);

                        NumberofCyclesLeft -= 7;
                        IncrementProgramCounter(3);
                        break;
                    }
                //DEX Decrement X Register by One, Implied, 1 Bytes, 2 Cycles
                case 0xCA:
                    {
                        ChangeRegisterByOne(true, true);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //DEY Decrement Y Register by One, Implied, 1 Bytes, 2 Cycles
                case 0x88:
                    {
                        ChangeRegisterByOne(false, true);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //INC Increment Memory by One, Zero Page, 2 Bytes, 5 Cycles
                case 0xE6:
                    {
                        ChangeMemoryByOne(AddressingMode.ZeroPage, false);

                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //INC Increment Memory by One, Zero Page X, 2 Bytes, 6 Cycles
                case 0xF6:
                    {
                        ChangeMemoryByOne(AddressingMode.ZeroPageX, false);

                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //INC Increment Memory by One, Absolute, 3 Bytes, 6 Cycles
                case 0xEE:
                    {
                        ChangeMemoryByOne(AddressingMode.Absolute, false);

                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(3);
                        break;
                    }
                //INC Increment Memory by One, Absolute X, 3 Bytes, 7 Cycles
                case 0xFE:
                    {
                        ChangeMemoryByOne(AddressingMode.AbsoluteX, false);

                        NumberofCyclesLeft -= 7;
                        IncrementProgramCounter(3);
                        break;
                    }
                //INX Increment X Register by One, Implied, 1 Bytes, 2 Cycles
                case 0xE8:
                    {
                        ChangeRegisterByOne(true, false);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //INY Increment Y Register by One, Implied, 1 Bytes, 2 Cycles
                case 0xC8:
                    {
                        ChangeRegisterByOne(false, false);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                #endregion

                #region GOTO and GOSUB Operations
                //JMP Jump to New Location, Absolute 3 Bytes, 3 Cycles
                case 0x4C:
                    {
                        ProgramCounter = GetAddressByAddressingMode(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 3;
                        break;
                    }
                //JMP Jump to New Location, Indirect 3 Bytes, 5 Cycles
                case 0x6C:
                    {

                        ProgramCounter = GetAddressByAddressingMode(AddressingMode.Absolute);
                        ProgramCounter = GetAddressByAddressingMode(AddressingMode.Absolute);

                        NumberofCyclesLeft -= 5;
                        break;
                    }
                //JSR Jump to SubRoutine, Absolute, 3 Bytes, 6 Cycles
                case 0x20:
                    {
                        JumpToSubRoutineOperation();

                        NumberofCyclesLeft -= 6;
                        break;
                    }
                //BRK Simulate IRQ, Implied, 1 Byte, 7 Cycles
                case 0x00:
                    {
                        BreakOperation(true, 0xFFFE);

                        NumberofCyclesLeft -= 7;
                        break;
                    }
                //RTI Return From Interrupt, Implied, 1 Byte, 6 Cycles
                case 0x40:
                    {
                        ReturnFromInterruptOperation();

                        NumberofCyclesLeft -= 6;
                        break;
                    }
                //RTS Return From Subroutine, Implied, 1 Byte, 6 Cycles
                case 0x60:
                    {
                        ReturnFromSubRoutineOperation();

                        NumberofCyclesLeft -= 6;
                        break;
                    }
                #endregion

                #region Load Value From Memory Operations
                //LDA Load Accumulator with Memory, Immediate, 2 Bytes, 2 Cycles
                case 0xA9:
                    {

                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Immediate));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDA Load Accumulator with Memory, Zero Page, 2 Bytes, 3 Cycles
                case 0xA5:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.ZeroPage));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDA Load Accumulator with Memory, Zero Page X, 2 Bytes, 4 Cycles
                case 0xB5:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.ZeroPageX));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDA Load Accumulator with Memory, Absolute, 3 Bytes, 4 Cycles
                case 0xAD:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Absolute));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LDA Load Accumulator with Memory, Absolute X, 3 Bytes, 4+ Cycles
                case 0xBD:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.AbsoluteX));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LDA Load Accumulator with Memory, Absolute Y, 3 Bytes, 4+ Cycles
                case 0xB9:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.AbsoluteY));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LDA Load Accumulator with Memory, Index Indirect, 2 Bytes, 6 Cycles
                case 0xA1:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.IndirectX));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDA Load Accumulator with Memory, Indirect Index, 2 Bytes, 5+ Cycles
                case 0xB1:
                    {
                        Accumulator = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.IndirectY));
                        SetZeroFlag(Accumulator);
                        SetNegativeFlag(Accumulator);

                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDX Load X with memory, Immediate, 2 Bytes, 2 Cycles
                case 0xA2:
                    {
                        XRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Immediate));
                        SetZeroFlag(XRegister);
                        SetNegativeFlag(XRegister);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDX Load X with memory, Zero Page, 2 Bytes, 3 Cycles
                case 0xA6:
                    {
                        XRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.ZeroPage));
                        SetZeroFlag(XRegister);
                        SetNegativeFlag(XRegister);

                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDX Load X with memory, Zero Page Y, 2 Bytes, 4 Cycles
                case 0xB6:
                    {
                        XRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.ZeroPageY));
                        SetZeroFlag(XRegister);
                        SetNegativeFlag(XRegister);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDX Load X with memory, Absolute, 3 Bytes, 4 Cycles
                case 0xAE:
                    {
                        XRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Absolute));
                        SetZeroFlag(XRegister);
                        SetNegativeFlag(XRegister);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LDX Load X with memory, Absolute Y, 3 Bytes, 4+ Cycles
                case 0xBE:
                    {
                        XRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.AbsoluteY));
                        SetZeroFlag(XRegister);
                        SetNegativeFlag(XRegister);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LDY Load Y with memory, Immediate, 2 Bytes, 2 Cycles
                case 0xA0:
                    {
                        YRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Immediate));
                        SetZeroFlag(YRegister);
                        SetNegativeFlag(YRegister);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDY Load Y with memory, Zero Page, 2 Bytes, 3 Cycles
                case 0xA4:
                    {
                        YRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.ZeroPage));
                        SetZeroFlag(YRegister);
                        SetNegativeFlag(YRegister);

                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDY Load Y with memory, Zero Page X, 2 Bytes, 4 Cycles
                case 0xB4:
                    {
                        YRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.ZeroPageX));
                        SetZeroFlag(YRegister);
                        SetNegativeFlag(YRegister);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LDY Load Y with memory, Absolute, 3 Bytes, 4 Cycles
                case 0xAC:
                    {
                        YRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Absolute));
                        SetZeroFlag(YRegister);
                        SetNegativeFlag(YRegister);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LDY Load Y with memory, Absolue X, 3 Bytes, 4+ Cycles
                case 0xBC:
                    {
                        YRegister = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.AbsoluteX));
                        SetZeroFlag(YRegister);
                        SetNegativeFlag(YRegister);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                #endregion

                #region Push/Pull Stack
                //PHA Push Accumulator onto Stack, Implied, 1 Byte, 3 Cycles
                case 0x48:
                    {
                        PokeStack((byte)Accumulator);
                        StackPointer--;

                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(1);
                        break;

                    }
                //PHP Push Flags onto Stack, Implied, 1 Byte, 3 Cycles
                case 0x08:
                    {
                        PushFlagsOperation();
                        StackPointer--;

                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(1);
                        break;
                    }
                //PLA Pull Accumulator from Stack, Implied, 1 Byte, 4 Cycles
                case 0x68:
                    {
                        StackPointer++;
                        Accumulator = PeekStack();

                        SetNegativeFlag(Accumulator);
                        SetZeroFlag(Accumulator);

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(1);
                        break;
                    }
                //PLP Pull Flags from Stack, Implied, 1 Byte, 4 Cycles
                case 0x28:
                    {
                        StackPointer++;
                        PullFlagsOperation();

                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(1);
                        break;
                    }
                //TSX Transfer Stack Pointer to X Register, 1 Bytes, 2 Cycles
                case 0xBA:
                    {
                        XRegister = StackPointer;

                        SetNegativeFlag(XRegister);
                        SetZeroFlag(XRegister);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //TXS Transfer X Register to Stack Pointer, 1 Bytes, 2 Cycles
                case 0x9A:
                    {
                        StackPointer = (byte)XRegister;

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                #endregion

                #region Set Flag Operations
                //SEC Set Carry, Implied, 1 Bytes, 2 Cycles
                case 0x38:
                    {
                        CarryFlag = true;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //SED Set Interrupt, Implied, 1 Bytes, 2 Cycles
                case 0xF8:
                    {
                        DecimalFlag = true;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //SEI Set Interrupt, Implied, 1 Bytes, 2 Cycles
                case 0x78:
                    {
                        DisableInterruptFlag = true;
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                #endregion

                #region Shift/Rotate Operations
                //ASL Shift Left 1 Bit Memory or Accumulator, Accumulator, 1 Bytes, 2 Cycles
                case 0x0A:
                    {
                        AslOperation(AddressingMode.Accumulator);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //ASL Shift Left 1 Bit Memory or Accumulator, Zero Page, 2 Bytes, 5 Cycles
                case 0x06:
                    {
                        AslOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ASL Shift Left 1 Bit Memory or Accumulator, Zero PageX, 2 Bytes, 6 Cycles
                case 0x16:
                    {
                        AslOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ASL Shift Left 1 Bit Memory or Accumulator, Absolute, 3 Bytes, 6 Cycles
                case 0x0E:
                    {
                        AslOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ASL Shift Left 1 Bit Memory or Accumulator, AbsoluteX, 3 Bytes, 7 Cycles
                case 0x1E:
                    {
                        AslOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 7;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LSR Shift Left 1 Bit Memory or Accumulator, Accumulator, 1 Bytes, 2 Cycles
                case 0x4A:
                    {
                        LsrOperation(AddressingMode.Accumulator);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //LSR Shift Left 1 Bit Memory or Accumulator, Zero Page, 2 Bytes, 5 Cycles
                case 0x46:
                    {
                        LsrOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LSR Shift Left 1 Bit Memory or Accumulator, Zero PageX, 2 Bytes, 6 Cycles
                case 0x56:
                    {
                        LsrOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //LSR Shift Left 1 Bit Memory or Accumulator, Absolute, 3 Bytes, 6 Cycles
                case 0x4E:
                    {
                        LsrOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(3);
                        break;
                    }
                //LSR Shift Left 1 Bit Memory or Accumulator, AbsoluteX, 3 Bytes, 7 Cycles
                case 0x5E:
                    {
                        LsrOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 7;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ROL Rotate Left 1 Bit Memory or Accumulator, Accumulator, 1 Bytes, 2 Cycles
                case 0x2A:
                    {
                        RolOperation(AddressingMode.Accumulator);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //ROL Rotate Left 1 Bit Memory or Accumulator, Zero Page, 2 Bytes, 5 Cycles
                case 0x26:
                    {
                        RolOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ROL Rotate Left 1 Bit Memory or Accumulator, Zero PageX, 2 Bytes, 6 Cycles
                case 0x36:
                    {
                        RolOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ROL Rotate Left 1 Bit Memory or Accumulator, Absolute, 3 Bytes, 6 Cycles
                case 0x2E:
                    {
                        RolOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ROL Rotate Left 1 Bit Memory or Accumulator, AbsoluteX, 3 Bytes, 7 Cycles
                case 0x3E:
                    {
                        RolOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 7;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ROR Rotate Right 1 Bit Memory or Accumulator, Accumulator, 1 Bytes, 2 Cycles
                case 0x6A:
                    {
                        RorOperation(AddressingMode.Accumulator);
                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //ROR Rotate Right 1 Bit Memory or Accumulator, Zero Page, 2 Bytes, 5 Cycles
                case 0x66:
                    {
                        RorOperation(AddressingMode.ZeroPage);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ROR Rotate Right 1 Bit Memory or Accumulator, Zero PageX, 2 Bytes, 6 Cycles
                case 0x76:
                    {
                        RorOperation(AddressingMode.ZeroPageX);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //ROR Rotate Right 1 Bit Memory or Accumulator, Absolute, 3 Bytes, 6 Cycles
                case 0x6E:
                    {
                        RorOperation(AddressingMode.Absolute);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(3);
                        break;
                    }
                //ROR Rotate Right 1 Bit Memory or Accumulator, AbsoluteX, 3 Bytes, 7 Cycles
                case 0x7E:
                    {
                        RorOperation(AddressingMode.AbsoluteX);
                        NumberofCyclesLeft -= 7;
                        IncrementProgramCounter(3);
                        break;
                    }
                #endregion

                #region Store Value In Memory Operations
                //STA Store Accumulator In Memory, Zero Page, 2 Bytes, 3 Cycles
                case 0x85:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.ZeroPage), (byte)Accumulator);
                        IncrementProgramCounter(2);
                        NumberofCyclesLeft -= 3;
                        break;
                    }
                //STA Store Accumulator In Memory, Zero Page X, 2 Bytes, 4 Cycles
                case 0x95:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.ZeroPageX), (byte)Accumulator);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STA Store Accumulator In Memory, Absolute, 3 Bytes, 4 Cycles
                case 0x8D:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.Absolute), (byte)Accumulator);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STA Store Accumulator In Memory, Absolute X, 3 Bytes, 5 Cycles
                case 0x9D:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.AbsoluteX), (byte)Accumulator);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STA Store Accumulator In Memory, Absolute Y, 3 Bytes, 5 Cycles
                case 0x99:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.AbsoluteY), (byte)Accumulator);
                        NumberofCyclesLeft -= 5;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STA Store Accumulator In Memory, Indexed Indirect, 2 Bytes, 6 Cycles
                case 0x81:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.IndirectX), (byte)Accumulator);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STA Store Accumulator In Memory, Indirect Indexed, 2 Bytes, 6 Cycles
                case 0x91:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.IndirectY), (byte)Accumulator);
                        NumberofCyclesLeft -= 6;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STX Store Index X, Zero Page, 2 Bytes, 3 Cycles
                case 0x86:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.ZeroPage), (byte)XRegister);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STX Store Index X, Zero Page Y, 2 Bytes, 4 Cycles
                case 0x96:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.ZeroPageY), (byte)XRegister);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STX Store Index X, Absolute, 3 Bytes, 4 Cycles
                case 0x8E:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.Absolute), (byte)XRegister);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                //STY Store Index Y, Zero Page, 2 Bytes, 3 Cycles
                case 0x84:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.ZeroPage), (byte)YRegister);
                        NumberofCyclesLeft -= 3;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STY Store Index Y, Zero Page X, 2 Bytes, 4 Cycles
                case 0x94:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.ZeroPageX), (byte)YRegister);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(2);
                        break;
                    }
                //STY Store Index Y, Absolute, 2 Bytes, 4 Cycles
                case 0x8C:
                    {
                        _core.WriteMemory(GetAddressByAddressingMode(AddressingMode.Absolute), (byte)YRegister);
                        NumberofCyclesLeft -= 4;
                        IncrementProgramCounter(3);
                        break;
                    }
                #endregion

                #region Transfer Operations
                //TAX Transfer Accumulator to X Register, Implied, 1 Bytes, 2 Cycles
                case 0xAA:
                    {
                        XRegister = Accumulator;

                        SetNegativeFlag(XRegister);
                        SetZeroFlag(XRegister);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //TAY Transfer Accumulator to Y Register, 1 Bytes, 2 Cycles
                case 0xA8:
                    {
                        YRegister = Accumulator;

                        SetNegativeFlag(YRegister);
                        SetZeroFlag(YRegister);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //TXA Transfer X Register to Accumulator, Implied, 1 Bytes, 2 Cycles
                case 0x8A:
                    {
                        Accumulator = XRegister;

                        SetNegativeFlag(Accumulator);
                        SetZeroFlag(Accumulator);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                //TYA Transfer Y Register to Accumulator, Implied, 1 Bytes, 2 Cycles
                case 0x98:
                    {
                        Accumulator = YRegister;

                        SetNegativeFlag(Accumulator);
                        SetZeroFlag(Accumulator);

                        NumberofCyclesLeft -= 2;
                        IncrementProgramCounter(1);
                        break;
                    }
                #endregion

                //NOP Operation, Implied, 1 Byte, 2 Cycles
                case 0xEA:
                    {
                        NumberofCyclesLeft -= 2;
                        break;
                    }

                default:
                    throw new NotSupportedException(string.Format("The OpCode {0} is not supported", CurrentOpCode));
            }
        }

        /// <summary>
        /// Increments the program Counter. We always Increment by 1 less than the value that is passed in to account for the increment that happens after the current
        /// Op code is retrieved
        /// </summary>
        /// <param name="lengthOfOperation">The lenght of the operation</param>
        private void IncrementProgramCounter(int lengthOfOperation)
        {
            //The PC gets increments after the opcode is retrieved but before the opcode is executed. We want to add the remaining length.
            ProgramCounter += lengthOfOperation - 1;
        }

        /// <summary>
        /// Sets the IsSignNegative register
        /// </summary>
        /// <param name="value"></param>
        private void SetNegativeFlag(int value)
        {
            //on the 6502, any value greater than 127 is negative. 128 = 1000000 in Binary. the 8th bit is set, therefore the number is a negative number.
            NegativeFlag = value > 127;
        }

        /// <summary>
        /// Sets the IsResultZero register
        /// </summary>
        /// <param name="value"></param>
        private void SetZeroFlag(int value)
        {
            ZeroFlag = value == 0;
        }

        /// <summary>
        /// Uses the AddressingMode to return the correct address based on the mode.
        /// Note: This method will not increment the program counter for any mode.
        /// Note: This method will return an error if called for either the immediate or accumulator modes. 
        /// </summary>
        /// <param name="addressingMode">The addressing Mode to use</param>
        /// <returns>The memory Location</returns>
        private int GetAddressByAddressingMode(AddressingMode addressingMode)
        {
            int address = 0;
            switch (addressingMode)
            {
                case (AddressingMode.Absolute):
                    {
                        //Get the first half of the address
                        address = _core.ReadMemory(ProgramCounter);

                        //Get the second half of the address. We multiple the value by 256 so we retrieve the right address. 
                        //IF the first Value is FF and the second value is FF the address would be FFFF.
                        address += 256 * _core.ReadMemory(ProgramCounter + 1);
                        return address;
                    }
                case AddressingMode.AbsoluteX:
                    {
                        //Get the first half of the address
                        address = _core.ReadMemory(ProgramCounter);

                        //Get the second half of the address. We multiple the value by 256 so we retrieve the right address. 
                        //IF the first Value is FF and the second value is FF the address would be FFFF.
                        //Then add the X Register value to that.
                        //We don't increment the program counter here, because it is incremented as part of the operation.
                        address += (256 * _core.ReadMemory(ProgramCounter + 1) + XRegister);

                        //This address wraps if its greater than 0xFFFF
                        if (address > 0xFFFF)
                        {
                            address -= 0x10000;
                            //We crossed a page boundry, so decrease the number of cycles by 1.
                            //However, if this is an ASL, LSR, DEC, INC, ROR, ROL or STA operation, we do not decrease it by 1.
                            switch (CurrentOpCode)
                            {
                                case 0x1E:
                                case 0xDE:
                                case 0xFE:
                                case 0x5E:
                                case 0x3E:
                                case 0x7E:
                                case 0x9D:
                                    {
                                        return address;
                                    }
                                default:
                                    {
                                        NumberofCyclesLeft--;
                                        break;
                                    }
                            }
                        }
                        return address;
                    }
                case AddressingMode.AbsoluteY:
                    {
                        //Get the first half of the address
                        address = _core.ReadMemory(ProgramCounter);

                        //Get the second half of the address. We multiple the value by 256 so we retrieve the right address. 
                        //IF the first Value is FF and the second value is FF the address would be FFFF.
                        //Then add the Y Register value to that.
                        //We don't increment the program counter here, because it is incremented as part of the operation.
                        address += (256 * _core.ReadMemory(ProgramCounter + 1) + YRegister);

                        //This address wraps if its greater than 0xFFFF
                        if (address > 0xFFFF)
                        {
                            address -= 0x10000;
                            //We crossed a page boundry, so decrease the number of cycles by 1 if the operation is not STA
                            if (CurrentOpCode != 0x99)
                                NumberofCyclesLeft--;
                        }
                        return address;
                    }
                case AddressingMode.Immediate:
                    {
                        return ProgramCounter;
                    }
                case AddressingMode.IndirectX:
                    {
                        //Get the location of the address to retrieve
                        address = _core.ReadMemory(ProgramCounter) + XRegister;

                        //Its a zero page address, so it wraps around if greater than 0xff
                        if (address > 0xff)
                            address -= 0x100;

                        //Now get the final Address. The is not a zero page address either.
                        var finalAddress = _core.ReadMemory(address) + (_core.ReadMemory(address + 1) << 8);
                        return finalAddress;
                    }
                case AddressingMode.IndirectY:
                    {
                        address = _core.ReadMemory(ProgramCounter);

                        var finalAddress = _core.ReadMemory(address) + (_core.ReadMemory(address + 1) << 8) + YRegister;

                        //This address wraps if its greater than 0xFFFF
                        if (finalAddress > 0xFFFF)
                        {
                            finalAddress -= 0x10000;

                            //We crossed a page boundry, so decrease the number of cycles by 1 if the operation is not STA
                            if (CurrentOpCode != 0x91)
                                NumberofCyclesLeft--;
                        }
                        return finalAddress;
                    }
                case AddressingMode.Relative:
                    {
                        return ProgramCounter;
                    }
                case (AddressingMode.ZeroPage):
                    {
                        address = _core.ReadMemory(ProgramCounter);
                        return address;
                    }
                case (AddressingMode.ZeroPageX):
                    {
                        address = _core.ReadMemory(ProgramCounter);
                        address += XRegister;

                        //This address wraps if its greater than 0xFFFF
                        if (address > 0xFF)
                        {
                            address -= 0x100;

                            //We crossed a page boundry, so decrease the number of cycles by 1.
                            //However, if this is an ASL, LSR, DEC, INC, ROR, ROL or STA operation, we do not decrease it by 1.
                            switch (CurrentOpCode)
                            {
                                case 0x1E:
                                case 0xDE:
                                case 0xFE:
                                case 0x5E:
                                case 0x3E:
                                case 0x7E:
                                case 0x9D:
                                    {
                                        return address;
                                    }
                                default:
                                    {
                                        NumberofCyclesLeft--;
                                        break;
                                    }
                            }
                        }

                        return address;
                    }
                case (AddressingMode.ZeroPageY):
                    {
                        address = _core.ReadMemory(ProgramCounter);

                        address += YRegister;

                        //This address wraps if its greater than 0xFFFF
                        if (address > 0xFF)
                        {
                            address -= 0x100;
                            //We crossed a page boundry, so decrease the number of cycles by 1 if the operation is not STA
                            if (CurrentOpCode != 0x99)
                                NumberofCyclesLeft--;
                        }

                        return address;
                    }
                default:
                    throw new InvalidOperationException(string.Format("The Address Mode '{0}' does not require an address", addressingMode));
            }
        }

        /// <summary>
        /// Moves the ProgramCounter in a given direction based on the value inputted
        /// 
        /// </summary>
        private int MoveProgramCounterByRelativeValue(byte valueToMove)
        {
            var movement = valueToMove > 127 ? (valueToMove - 255) : valueToMove;

            var newProgramCounter = ProgramCounter + movement;

            //This makes sure that we always land on the correct spot for a positive number
            if (movement >= 0)
                newProgramCounter++;

            if (newProgramCounter < 0x0 || newProgramCounter > 0xFFFF)
            {
                //We crossed a page boundry, so decrease the number of cycles by 1.
                NumberofCyclesLeft--;
            }
            return newProgramCounter;
        }

        /// <summary>
        /// Returns a the value from the stack without changing the position of the stack pointer
        /// </summary>

        /// <returns>The value at the current Stack Pointer</returns>
        private byte PeekStack()
        {
            //The stack lives at 0x100-0x1FF, but the value is only a byte so it needs to be translated
            return _core.ReadMemory(StackPointer + 0x100);
        }

        /// <summary>
        /// Write a value directly to the stack without modifying the Stack Pointer
        /// </summary>
        /// <param name="value">The value to be written to the stack</param>
        private void PokeStack(byte value)
        {
            //The stack lives at 0x100-0x1FF, but the value is only a byte so it needs to be translated
            _core.WriteMemory(StackPointer + 0x100, value);
        }

        /// <summary>
        /// Coverts the Flags into its byte representation.
        /// </summary>
        /// <param name="setBreak">Determines if the break flag should be set during conversion. IRQ does not set the flag on the stack, but PHP and BRK do</param>
        /// <returns></returns>
        private byte ConvertFlagsToByte(bool setBreak)
        {
            return (byte)((CarryFlag ? 0x01 : 0) + (ZeroFlag ? 0x02 : 0) + (DisableInterruptFlag ? 0x04 : 0) +
                         (DecimalFlag ? 8 : 0) + (setBreak ? 0x10 : 0) + 0x20 + (OverflowFlag ? 0x40 : 0) + (NegativeFlag ? 0x80 : 0));
        }

        private void SetDisassembly()
        {
            var addressMode = GetAddressingMode();

            var currentProgramCounter = ProgramCounter;

            currentProgramCounter = WrapProgramCounter(++currentProgramCounter);
            int? address1 = _core.ReadMemory(currentProgramCounter);

            currentProgramCounter = WrapProgramCounter(++currentProgramCounter);
            int? address2 = _core.ReadMemory(currentProgramCounter);


            string disassembledStep = string.Empty;

            switch (addressMode)
            {
                case AddressingMode.Absolute:
                    {
                        disassembledStep = string.Format("${0}{1}", address2.Value.ToString("X").PadLeft(2, '0'), address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.AbsoluteX:
                    {
                        disassembledStep = string.Format("${0}{1},X", address2.Value.ToString("X").PadLeft(2, '0'), address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.AbsoluteY:
                    {
                        disassembledStep = string.Format("${0}{1},Y", address2.Value.ToString("X").PadLeft(2, '0'), address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.Accumulator:
                    {
                        address1 = null;
                        address2 = null;

                        disassembledStep = "A";
                        break;
                    }
                case AddressingMode.Immediate:
                    {
                        disassembledStep = string.Format("#${0}", address1.Value.ToString("X").PadLeft(4, '0'));
                        address2 = null;
                        break;
                    }
                case AddressingMode.Implied:
                    {
                        address1 = null;
                        address2 = null;
                        break;
                    }
                case AddressingMode.Indirect:
                    {
                        disassembledStep = string.Format("(${0}{1})", address2.Value.ToString("X").PadLeft(2, '0'), address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.IndirectX:
                    {
                        address2 = null;

                        disassembledStep = string.Format("(${0},X)", address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.IndirectY:
                    {
                        address2 = null;

                        disassembledStep = string.Format("(${0}),Y", address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.Relative:
                    {
                        var relativeAddress = MoveProgramCounterByRelativeValue((byte)address1.Value);
                        relativeAddress = WrapProgramCounter(relativeAddress);

                        var stringAddress = relativeAddress.ToString("X").PadLeft(4, '0');

                        address1 = int.Parse(stringAddress.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                        address2 = int.Parse(stringAddress.Substring(2, 2), NumberStyles.AllowHexSpecifier);

                        disassembledStep = string.Format("${0}", relativeAddress.ToString("X").PadLeft(4, '0'));
                        break;
                    }
                case AddressingMode.ZeroPage:
                    {
                        address2 = null;

                        disassembledStep = string.Format("${0}", address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.ZeroPageX:
                    {
                        address2 = null;

                        disassembledStep = string.Format("${0},X", address1.Value.ToString("X").PadLeft(2, '0'));
                        break;
                    }
                case AddressingMode.ZeroPageY:
                    {
                        address2 = null;

                        disassembledStep = string.Format("${0},Y", address1.Value.ToString("X").PadLeft(4, '0'));
                        break;
                    }
                default:
                    throw new InvalidEnumArgumentException("Invalid Addressing Mode");

            }
   
        }

        private int WrapProgramCounter(int value)
        {
            if (value > 0xFFFF)
                value = value - 0x10000;
            else if (value < 0)
                value = value + 0x10000;

            return value;
        }

        private AddressingMode GetAddressingMode()
        {
            switch (CurrentOpCode)
            {
                case 0x0D: //ORA
                case 0x2D: //AND
                case 0x4D: //EOR
                case 0x6D: //ADC
                case 0x8D: //STA
                case 0xAD: //LDA
                case 0xCD: //CMP
                case 0xED: //SBC
                case 0x0E: //ASL
                case 0x2E: //ROL
                case 0x4E: //LSR
                case 0x6E: //ROR
                case 0x8E: //SDX
                case 0xAE: //LDX
                case 0xCE: //DEC
                case 0xEE: //INC
                case 0x2C: //Bit
                case 0x4C: //JMP
                case 0x8C: //STY
                case 0xAC: //LDY
                case 0xCC: //CPY
                case 0xEC: //CPX
                case 0x20: //JSR
                    {
                        return AddressingMode.Absolute;
                    }
                case 0x1D: //ORA
                case 0x3D: //AND
                case 0x5D: //EOR
                case 0x7D: //ADC
                case 0x9D: //STA
                case 0xBD: //LDA
                case 0xDD: //CMP
                case 0xFD: //SBC
                case 0xBC: //LDY
                case 0xFE: //INC
                case 0x1E: //ASL
                case 0x3E: //ROL
                case 0x5E: //LSR
                case 0x7E: //ROR
                    {
                        return AddressingMode.AbsoluteX;
                    }
                case 0x19: //ORA
                case 0x39: //AND
                case 0x59: //EOR
                case 0x79: //ADC
                case 0x99: //STA
                case 0xB9: //LDA
                case 0xD9: //CMP
                case 0xF9: //SBC
                case 0xBE: //LDX
                    {
                        return AddressingMode.AbsoluteY;
                    }
                case 0x0A: //ASL
                case 0x4A: //LSR
                case 0x2A: //ROL
                case 0x6A: //ROR
                    {
                        return AddressingMode.Accumulator;
                    }

                case 0x09: //ORA
                case 0x29: //AND
                case 0x49: //EOR
                case 0x69: //ADC
                case 0xA0: //LDY
                case 0xC0: //CPY
                case 0xE0: //CMP
                case 0xA2: //LDX
                case 0xA9: //LDA
                case 0xC9: //CMP
                case 0xE9: //SBC
                    {
                        return AddressingMode.Immediate;
                    }
                case 0x00: //BRK
                case 0x18: //CLC
                case 0xD8: //CLD
                case 0x58: //CLI
                case 0xB8: //CLV
                case 0xDE: //DEC
                case 0xCA: //DEX
                case 0x88: //DEY
                case 0xE8: //INX
                case 0xC8: //INY
                case 0xEA: //NOP
                case 0x48: //PHA
                case 0x08: //PHP
                case 0x68: //PLA
                case 0x28: //PLP
                case 0x40: //RTI
                case 0x60: //RTS
                case 0x38: //SEC
                case 0xF8: //SED
                case 0x78: //SEI
                case 0xAA: //TAX
                case 0xA8: //TAY
                case 0xBA: //TSX
                case 0x8A: //TXA
                case 0x9A: //TXS
                case 0x98: //TYA
                    {
                        return AddressingMode.Implied;
                    }
                case 0x6C:
                    {
                        return AddressingMode.Indirect;
                    }

                case 0x61: //ADC
                case 0x21: //AND
                case 0xC1: //CMP
                case 0x41: //EOR
                case 0xA1: //LDA
                case 0x01: //ORA
                case 0xE1: //SBC
                case 0x81: //STA
                    {
                        return AddressingMode.IndirectX;
                    }
                case 0x71: //ADC
                case 0x31: //AND
                case 0xD1: //CMP
                case 0x51: //EOR
                case 0xB1: //LDA
                case 0x11: //ORA
                case 0xF1: //SBC
                case 0x91: //STA
                    {
                        return AddressingMode.IndirectY;
                    }
                case 0x90: //BCC
                case 0xB0: //BCS
                case 0xF0: //BEQ
                case 0x30: //BMI
                case 0xD0: //BNE
                case 0x10: //BPL
                case 0x50: //BVC
                case 0x70: //BVS
                    {
                        return AddressingMode.Relative;
                    }
                case 0x65: //ADC
                case 0x25: //AND
                case 0x06: //ASL
                case 0x24: //BIT
                case 0xC5: //CMP
                case 0xE4: //CPX
                case 0xC4: //CPY
                case 0xC6: //DEC
                case 0x45: //EOR
                case 0xE6: //INC
                case 0xA5: //LDA
                case 0xA6: //LDX
                case 0xA4: //LDY
                case 0x46: //LSR
                case 0x05: //ORA
                case 0x26: //ROL
                case 0x66: //ROR
                case 0xE5: //SBC
                case 0x85: //STA
                case 0x86: //STX
                case 0x84: //STY
                    {
                        return AddressingMode.ZeroPage;
                    }
                case 0x75: //ADC
                case 0x35: //AND
                case 0x16: //ASL
                case 0xD5: //CMP
                case 0xD6: //DEC
                case 0x55: //EOR
                case 0xF6: //INC
                case 0xB5: //LDA
                case 0xB6: //LDX
                case 0xB4: //LDY
                case 0x56: //LSR
                case 0x15: //ORA
                case 0x36: //ROL
                case 0x76: //ROR
                case 0xF5: //SBC
                case 0x95: //STA
                case 0x96: //STX
                case 0x94: //STY
                    {
                        return AddressingMode.ZeroPageX;
                    }
                default:
                    throw new InvalidEnumArgumentException(string.Format("Unable to find Opcode {0} when Looking up Addressing Mode", CurrentOpCode));
            }
        }

        #region Op Code Operations
        /// <summary>
        /// The ADC - Add Memory to Accumulator with Carry Operation
        /// </summary>
        /// <param name="addressingMode">The addressing mode used to perform this operation.</param>
        private void AddWithCarryOperation(AddressingMode addressingMode)
        {
            //Accumulator, Carry = Accumulator + ValueInMemoryLocation + Carry 
            var memoryValue = _core.ReadMemory(GetAddressByAddressingMode(addressingMode));
            var newValue = memoryValue + Accumulator + (CarryFlag ? 1 : 0);


            OverflowFlag = (((Accumulator ^ newValue) & 0x80) != 0) && (((Accumulator ^ memoryValue) & 0x80) == 0);

            if (DecimalFlag)
            {
                newValue = int.Parse(memoryValue.ToString("x")) + int.Parse(Accumulator.ToString("x")) + (CarryFlag ? 1 : 0);

                if (newValue > 99)
                {
                    CarryFlag = true;
                    newValue -= 100;
                }
                else
                {
                    CarryFlag = false;
                }

                newValue = (int)Convert.ToInt64(string.Concat("0x", newValue), 16);
            }
            else
            {
                if (newValue > 255)
                {
                    CarryFlag = true;
                    newValue -= 256;
                }
                else
                {
                    CarryFlag = false;
                }
            }

            SetZeroFlag(newValue);
            SetNegativeFlag(newValue);

            Accumulator = newValue;
        }

        /// <summary>
        /// The AND - Compare Memory with Accumulator operation
        /// </summary>
        /// <param name="addressingMode">The addressing mode being used</param>
        private void AndOperation(AddressingMode addressingMode)
        {
            Accumulator = _core.ReadMemory(GetAddressByAddressingMode(addressingMode)) & Accumulator;

            SetZeroFlag(Accumulator);
            SetNegativeFlag(Accumulator);
        }

        /// <summary>
        /// The ASL - Shift Left One Bit (Memory or Accumulator)
        /// </summary>
        /// <param name="addressingMode">The addressing Mode being used</param>
        public void AslOperation(AddressingMode addressingMode)
        {
            int value = 0;
            var memoryAddress = 0;
            if (addressingMode == AddressingMode.Accumulator)
                value = Accumulator;
            else
            {
                memoryAddress = GetAddressByAddressingMode(addressingMode);
                value = _core.ReadMemory(memoryAddress);
            }

            //If the 7th bit is set, then we have a carry
            CarryFlag = ((value & 0x80) != 0);

            //The And here ensures that if the value is greater than 255 it wraps properly.
            value = (value << 1) & 0xFE;

            SetNegativeFlag(value);
            SetZeroFlag(value);

            if (addressingMode == AddressingMode.Accumulator)
                Accumulator = value;
            else
            {
                _core.WriteMemory(memoryAddress, (byte)value);
            }
        }

        /// <summary>
        /// Performs the different branch operations.
        /// </summary>
        /// <param name="performBranch">Is a branch required</param>
        private void BranchOperation(bool performBranch)
        {
            if (!performBranch)
            {
                IncrementProgramCounter(2);
                return;
            }

            var value = _core.ReadMemory(GetAddressByAddressingMode(AddressingMode.Relative));

            ProgramCounter = MoveProgramCounterByRelativeValue(value);
            //We add a cycle because the branch occured.
            NumberofCyclesLeft -= 1;

            //IncrementProgramCounter(2);
        }

        /// <summary>
        /// The bit operation, does an & comparison between a value in memory and the accumulator
        /// </summary>
        /// <param name="addressingMode"></param>
        private void BitOperation(AddressingMode addressingMode)
        {

            var memoryValue = _core.ReadMemory(GetAddressByAddressingMode(addressingMode));
            var valueToCompare = memoryValue & Accumulator;

            OverflowFlag = (memoryValue & 0x40) != 0;

            SetNegativeFlag(memoryValue);
            SetZeroFlag(valueToCompare);
        }

        /// <summary>
        /// The compare operation. This operation compares a value in memory with a value passed into it.
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        /// <param name="comparisonValue">The value to compare against memory</param>
        private void CompareOperation(AddressingMode addressingMode, int comparisonValue)
        {
            var memoryValue = _core.ReadMemory(GetAddressByAddressingMode(addressingMode));
            var comparedValue = comparisonValue - memoryValue;

            if (comparedValue < 0)
                comparedValue += 0x10000;

            SetZeroFlag(comparedValue);

            CarryFlag = memoryValue <= comparisonValue;
            SetNegativeFlag(comparedValue);
        }

        /// <summary>
        /// Changes a value in memory by 1
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        /// <param name="decrement">If the operation is decrementing or incrementing the vaulue by 1 </param>
        private void ChangeMemoryByOne(AddressingMode addressingMode, bool decrement)
        {
            var memoryLocation = GetAddressByAddressingMode(addressingMode);
            var memory = _core.ReadMemory(memoryLocation);

            if (decrement)
                memory -= 1;
            else
                memory += 1;

            SetZeroFlag(memory);
            SetNegativeFlag(memory);

            _core.WriteMemory(memoryLocation, memory);
        }

        /// <summary>
        /// Changes a value in either the X or Y register by 1
        /// </summary>
        /// <param name="useXRegister">If the operation is using the X or Y register</param>
        /// <param name="decrement">If the operation is decrementing or incrementing the vaulue by 1 </param>
        private void ChangeRegisterByOne(bool useXRegister, bool decrement)
        {
            var value = useXRegister ? XRegister : YRegister;

            if (decrement)
                value -= 1;
            else
                value += 1;

            if (value < 0x00)
                value += 0x100;
            else if (value > 0xFF)
                value -= 0x100;

            SetZeroFlag(value);
            SetNegativeFlag(value);

            if (useXRegister)
                XRegister = value;
            else
                YRegister = value;
        }

        /// <summary>
        /// The EOR Operation, Performs an Exclusive OR Operation against the Accumulator and a value in memory
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        private void EorOperation(AddressingMode addressingMode)
        {
            Accumulator = Accumulator ^ _core.ReadMemory(GetAddressByAddressingMode(addressingMode));

            SetNegativeFlag(Accumulator);
            SetZeroFlag(Accumulator);
        }

        /// <summary>
        /// The LSR Operation. Performs a Left shift operation on a value in memory
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        private void LsrOperation(AddressingMode addressingMode)
        {
            int value = 0;
            var memoryAddress = 0;
            if (addressingMode == AddressingMode.Accumulator)
                value = Accumulator;
            else
            {
                memoryAddress = GetAddressByAddressingMode(addressingMode);
                value = _core.ReadMemory(memoryAddress);
            }

            NegativeFlag = false;

            //If the Zero bit is set, we have a carry
            CarryFlag = (value & 0x01) != 0;

            value = (value >> 1);

            SetZeroFlag(value);

            if (addressingMode == AddressingMode.Accumulator)
                Accumulator = value;
            else
            {
                _core.WriteMemory(memoryAddress, (byte)value);
            }
        }

        /// <summary>
        /// The Or Operation. Performs an Or Operation with the accumulator and a value in memory
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        private void OrOperation(AddressingMode addressingMode)
        {
            Accumulator = Accumulator | _core.ReadMemory(GetAddressByAddressingMode(addressingMode));

            SetNegativeFlag(Accumulator);
            SetZeroFlag(Accumulator);
        }

        /// <summary>
        /// The ROL operation. Performs a rotate left operation on a value in memory.
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        private void RolOperation(AddressingMode addressingMode)
        {
            int value = 0;
            var memoryAddress = 0;
            if (addressingMode == AddressingMode.Accumulator)
                value = Accumulator;
            else
            {
                memoryAddress = GetAddressByAddressingMode(addressingMode);
                value = _core.ReadMemory(memoryAddress);
            }

            //Store the carry flag before shifting it
            var newCarry = (0x80 & value) != 0;

            //The And here ensures that if the value is greater than 255 it wraps properly.
            value = (value << 1) & 0xFE;

            if (CarryFlag)
                value = value | 0x01;

            CarryFlag = newCarry;

            SetZeroFlag(value);
            SetNegativeFlag(value);

            if (addressingMode == AddressingMode.Accumulator)
                Accumulator = value;
            else
            {
                _core.WriteMemory(memoryAddress, (byte)value);
            }
        }

        /// <summary>
        /// The ROR operation. Performs a rotate right operation on a value in memory.
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        private void RorOperation(AddressingMode addressingMode)
        {
            int value = 0;
            var memoryAddress = 0;
            if (addressingMode == AddressingMode.Accumulator)
                value = Accumulator;
            else
            {
                memoryAddress = GetAddressByAddressingMode(addressingMode);
                value = _core.ReadMemory(memoryAddress);
            }

            //Store the carry flag before shifting it
            var newCarry = (0x01 & value) != 0;

            value = (value >> 1);

            //If the carry flag is set then 0x
            if (CarryFlag)
                value = value | 0x80;

            CarryFlag = newCarry;

            SetZeroFlag(value);
            SetNegativeFlag(value);

            if (addressingMode == AddressingMode.Accumulator)
                Accumulator = value;
            else
            {
                _core.WriteMemory(memoryAddress, (byte)value);
            }
        }

        /// <summary>
        /// The SBC operation. Performs a subtract with carry operation on the accumulator and a value in memory.
        /// </summary>
        /// <param name="addressingMode">The addressing mode to use</param>
        private void SubtractWithBorrowOperation(AddressingMode addressingMode)
        {
            var memoryValue = _core.ReadMemory(GetAddressByAddressingMode(addressingMode));
            var newValue = DecimalFlag
                               ? int.Parse(Accumulator.ToString("x")) - int.Parse(memoryValue.ToString("x")) - (CarryFlag ? 0 : 1)
                               : Accumulator - memoryValue - (CarryFlag ? 0 : 1);

            CarryFlag = newValue >= 0;

            if (DecimalFlag)
            {
                if (newValue < 0)
                    newValue += 100;

                newValue = (int)Convert.ToInt64(string.Concat("0x", newValue), 16);
            }
            else
            {
                OverflowFlag = (((Accumulator ^ newValue) & 0x80) != 0) && (((Accumulator ^ memoryValue) & 0x80) != 0);

                if (newValue < 0)
                    newValue += 256;
            }

            SetNegativeFlag(newValue);
            SetZeroFlag(newValue);

            Accumulator = newValue;
        }

        /// <summary>
        /// The PSP Operation. Pushes the Status Flags to the stack
        /// </summary>
        private void PushFlagsOperation()
        {
            PokeStack(ConvertFlagsToByte(true));
        }

        /// <summary>
        /// The PLP Operation. Pull the status flags off the stack on sets the flags accordingly.
        /// </summary>
        private void PullFlagsOperation()
        {
            var flags = PeekStack();
            CarryFlag = (flags & 0x01) != 0;
            ZeroFlag = (flags & 0x02) != 0;
            DisableInterruptFlag = (flags & 0x04) != 0;
            DecimalFlag = (flags & 0x08) != 0;
            OverflowFlag = (flags & 0x40) != 0;
            NegativeFlag = (flags & 0x80) != 0;


        }

        /// <summary>
        /// The JSR routine. Jumps to a subroutine. 
        /// </summary>
        private void JumpToSubRoutineOperation()
        {
            //Put the high value on the stack, this should be the address after our operation -1
            //The RTS operation increments the PC by 1 which is why we don't move 2
            PokeStack((byte)(((ProgramCounter + 1) >> 8) & 0xFF));

            StackPointer--;

            PokeStack((byte)((ProgramCounter + 1) & 0xFF));

            StackPointer--;

            ProgramCounter = GetAddressByAddressingMode(AddressingMode.Absolute);

        }

        /// <summary>
        /// The RTS routine. Called when returning from a subroutine.
        /// </summary>
        private void ReturnFromSubRoutineOperation()
        {
            StackPointer++;

            var lowBit = PeekStack();

            StackPointer++;

            var highBit = PeekStack() << 8;

            ProgramCounter = (highBit | lowBit) + 1;
        }

        /// <summary>
        /// The BRK routine. Called when a BRK occurs.
        /// </summary>
        private void BreakOperation(bool isBrk, int vector)
        {
            //Put the high value on the stack
            //When we RTI the address will be incremented by one, and the address after a break will not be used.
            PokeStack((byte)(((ProgramCounter + 1) >> 8) & 0xFF));

            StackPointer--;

            //Put the low value on the stack
            PokeStack((byte)((ProgramCounter + 1) & 0xFF));

            StackPointer--;

            //We only set the Break Flag is a Break Occurs
            if (isBrk)
                PokeStack((byte)(ConvertFlagsToByte(true) | 0x10));
            else
                PokeStack(ConvertFlagsToByte(false));

            StackPointer--;

            DisableInterruptFlag = true;

            ProgramCounter = (_core.ReadMemory(vector + 1) << 8) | _core.ReadMemory(vector);
        }

        /// <summary>
        /// The RTI routine. Called when returning from a BRK opertion.
        /// Note: when called after a BRK operation the Program Counter is not set to the location after the BRK,
        /// it is set +1
        /// </summary>
        private void ReturnFromInterruptOperation()
        {
            StackPointer++;

            PullFlagsOperation();

            StackPointer++;

            var lowBit = PeekStack();

            StackPointer++;

            var highBit = PeekStack() << 8;

            ProgramCounter = (highBit | lowBit);
        }
        #endregion

        #endregion
    }


    public enum AddressingMode
    {
        /// <summary>
        /// In this mode a full address is given to operation on IE: Memory byte[] { 0x60, 0x00, 0xFF } 
        /// would perform an ADC operation and Add the value at ADDRESS 0xFF00 to the accumulator.
        /// The address is always LSB first
        /// </summary>
        Absolute = 1,
        /// <summary>
        /// In this mode a full address is given to operation on IE: Memory byte[] { 0x7D, 0x00, 0xFF } The full value would then be added to the X Register.
        /// If the X register was 0x01 then the address would be 0xFF01. and the value stored there would have an ADC operation performed on it and the value would
        /// be added to the accumulator.
        /// </summary>
        AbsoluteX = 2,
        /// <summary>
        /// In this mode a full address is given to operation on IE: Memory byte[] { 0x79, 0x00, 0xFF } The full value would then be added to the Y Register.
        /// If the Y register was 0x01 then the address would be 0xFF01. and the value stored there would have an ADC operation performed on it and the value would
        /// be added to the accumulator
        /// </summary>
        AbsoluteY = 3,
        /// <summary>
        /// In this mode the instruction operates on the accumulator. No operands are needed. 
        /// </summary>
        Accumulator = 4,
        /// <summary>
        /// In this mode, the value to operate on immediately follows the instruction. IE: Memory byte[] { 0x69, 0x01 } 
        /// would perform an ADC operation and Add 0x01 directly to the accumulator
        /// </summary>
        Immediate = 5,
        /// <summary>
        /// No address is needed for this mode. EX: BRK (Break), CLC (Clear Carry Flag) etc
        /// </summary>
        Implied = 6,
        /// <summary>
        /// In this mode assume the following
        /// Memory = { 0x61, 0x02, 0x04, 0x00, 0x03 }
        /// RegisterX = 0x01
        /// 1. Take the sum of the X Register and the value after the opcode 0x01 + 0x01 = 0x02. 
        /// 2. Starting at position 0x02 get an address (0x04,0x00) = 0x0004
        /// 3. Perform the ADC operation and Add the value at 0x0005 to the accumulator
        /// Note: if the Zero Page address is greater than 0xff then roll over the value. IE 0x101 rolls over to 0x01
        /// </summary>
        IndirectX = 7,
        /// <summary>
        /// In this mode assume the following
        /// Memory = { 0x61, 0x02, 0x04, 0x00, 0x03 }
        /// RegisterY = 0x01
        /// 1. Starting at position 0x02 get an address (0x04,0x00) = 0x0004 
        /// 2. Take the sum of the Y Register and the absolute address 0x01+0x0004 = 0x0005
        /// 3. Perform the ADC operation and Add the value at 0x0005 to the accumulator
        /// Note: if the address is great that 0xffff then roll over IE: 0x10001 rolls over to 0x01
        /// </summary>
        IndirectY = 8,
        /// <summary>
        /// JMP is the only operation that uses this mode. In this mode an absolute address is specified that points to the location of the absolute address we want to jump to.
        /// </summary>
        Indirect = 9,
        /// <summary>
        /// This Mode Changes the PC. It allows the program to change the location of the PC by 127 in either direction.
        /// </summary>
        Relative = 10,
        /// <summary>
        /// In this mode, a zero page address of the value to operate on is specified. This mode can only operation on values between 0x0 and 0xFF, or those that sit on the zero page of memory. IE: Memory byte[] { 0x69, 0x02, 0x01 } 
        /// would perform an ADC operation and Add 0x01 directly to the Accumulator
        /// </summary>
        ZeroPage = 11,
        /// <summary>
        /// In this mode, a zero page address of the value to operate on is specified, however the value of the X register is added to the address IE: Memory byte[] { 0x86, 0x02, 0x01, 0x67, 0x04, 0x01 } 
        /// In this example we store a value of 0x01 into the X register, then we would perform an ADC operation using the addres of 0x04+0x01=0x05 and Add the result of 0x01 directly to the Accumulator
        /// </summary>
        ZeroPageX = 12,
        /// <summary>
        /// This works the same as ZeroPageX except it uses the Y register instead of the X register.
        /// </summary>
        ZeroPageY = 13,
    }
}

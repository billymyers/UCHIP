﻿using System;

namespace Chip8
{

    public class Chip8
    {
        // The memory of the Chip-8 system
        private readonly byte[] _memory = new byte[0x1000];

        // The default fontset used by the Chip-8.
        private static readonly byte[] FontSet =
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, //0
            0x20, 0x60, 0x20, 0x20, 0x70, //1
            0xF0, 0x10, 0xF0, 0x80, 0xF0, //2
            0xF0, 0x10, 0xF0, 0x10, 0xF0, //3
            0x90, 0x90, 0xF0, 0x10, 0x10, //4
            0xF0, 0x80, 0xF0, 0x10, 0xF0, //5
            0xF0, 0x80, 0xF0, 0x90, 0xF0, //6
            0xF0, 0x10, 0x20, 0x40, 0x40, //7
            0xF0, 0x90, 0xF0, 0x90, 0xF0, //8
            0xF0, 0x90, 0xF0, 0x10, 0xF0, //9
            0xF0, 0x90, 0xF0, 0x90, 0x90, //A
            0xE0, 0x90, 0xE0, 0x90, 0xE0, //B
            0xF0, 0x80, 0x80, 0x80, 0xF0, //C
            0xE0, 0x90, 0x90, 0x90, 0xE0, //D
            0xF0, 0x80, 0xF0, 0x80, 0xF0, //E
            0xF0, 0x80, 0xF0, 0x80, 0x80 //F
        };

        // Registers and timers.
        public byte[] V = new byte[0x10];
        public byte Delay;
        public byte Sound;
        public ushort I;
        public ushort PC;
        public byte StackPointer;
        public ushort[] Stack = new ushort[0x10];
        public byte[,] Display = new byte[64, 32];
        public bool[] Input = new bool[0x10];

        // Determines whether the machine is powered or not.
        public bool Powered;

        // Settings for our Chip8 emulation, allowing for different programs written for different interpreters.
        // Wrapping mode of the CHIP-8. Most implementations wrap sprite display, but some don't. By default this is set to the wrapping mode.
        public Chip8WrappingMode WrappingMode = Chip8WrappingMode.Wrap;

        // The interpreter mode. There are differences between the SCHIP mode and the COSMAC mode. All information regarding
        // these two different modes can be found here: https://github.com/mattmikolay/chip-8/wiki/CHIP%E2%80%908-Instruction-Set
        public Chip8InterpreterMode InterpreterMode = Chip8InterpreterMode.Schip;

        // The current opcode.
        private ushort _opcode;

        // This is a value used to increment the PC correctly.
        private ushort _next;

        // Determines whether the screen should be redrawn or not. Usually only 00E0 and DXYN opcodes should set this to true.
        public bool Draw { get; private set; }

        public void Update()
        {
            Cycle();
        }

        /// <summary>
        ///     Loads a ROM into memory, then powers the virtual machine.
        /// </summary>
        /// <param name="romData">The ROM data stored as a byte array.</param>
        public void PowerAndLoadRom(byte[] romData)
        {
            Powered = false;
            Power();
            // Load ROM into memory
            for (var i = 0; i < romData.Length; i++) _memory[0x200 + i] = romData[i];
            Powered = true;
        }

        /// <summary>
        ///     Sets up the virtual machine to ensure that it is in the correct operational state before running any program.
        /// </summary>
        private void Power()
        {
            // Reset pc, opcode reading, I, and sp
            PC = 0x200;
            _opcode = I = StackPointer = 0;

            // Clear memory, display, stack, and registers
            for (var i = 0; i < 0x1000; i++)
            {
                _memory[i] = 0;
                if (i < 0x10)
                {
                    Stack[i] = 0;
                    V[i] = 0;
                    Input[i] = false;
                }
            }

            // Clear display
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 64; x++)
                Display[x, y] = 0;

            for (var i = 0; i < 80; i++) _memory[i] = FontSet[i];

            // Reset timers
            Delay = Sound = 0;
        }

        /// <summary>
        ///     The heart of the Chip-8 interpreter. Emulates a cycle of the interpreter, and executes an opcode.
        /// </summary>
        /// <exception cref="IllegalOpcodeException">Thrown upon encountering an unexpected or unsupported opcode.</exception>
        private void Cycle()
        {
            // Reset draw flag to false
            Draw = false;
            _opcode = (ushort) ((_memory[PC] << 8) | _memory[PC + 1]); // Turns two bytes into a single short
            // Determine the opcode to run
            switch (_opcode & 0xF000)
            {
                case 0x0000:
                    switch (_opcode & 0x0FFF)
                    {
                        case 0xE0:
                            // 0x00E0 - clears screen
                            ClearScreen();
                            break;
                        case 0xEE:
                            // 0x00EE - return from subroutine
                            ReturnFromSubroutine();
                            break;
                        default:
                            // Illegal opcode occured
                            throw new IllegalOpcodeException("Illegal opcode called in emulation!", _opcode);
                    }

                    break;
                case 0x1000:
                    // Jump to address
                    JumpToAddress((ushort) (_opcode & 0xFFF));
                    break;
                case 0x2000:
                    // Call subroutine
                    CallSubroutine((ushort) (_opcode & 0xFFF));
                    break;
                case 0x3000:
                    // Skip next instruction if equal
                    SkipInstruction((_opcode & 0xF00) >> 8, (byte) _opcode, true);
                    break;
                case 0x4000:
                    // Skip next instruction if equal
                    SkipInstruction((_opcode & 0xF00) >> 8, (byte) _opcode, false);
                    break;
                case 0x5000:
                    SkipInstruction((_opcode & 0xF00) >> 8, V[(_opcode & 0xF0) >> 4], true);
                    break;
                case 0x6000:
                    SetRegister((_opcode & 0xF00) >> 8, (byte) _opcode);
                    break;
                case 0x7000:
                    AddRegister((_opcode & 0xF00) >> 8, (byte) _opcode);
                    break;
                case 0x8000:
                    switch (_opcode & 0xF)
                    {
                        case 0x0:
                            V[(_opcode & 0xF00) >> 8] = V[(_opcode & 0xF0) >> 4];
                            _next = 2;
                            break;
                        case 0x1:
                            V[(_opcode & 0xF00) >> 8] |= V[(_opcode & 0xF0) >> 4];
                            _next = 2;
                            break;
                        case 0x2:
                            V[(_opcode & 0xF00) >> 8] &= V[(_opcode & 0xF0) >> 4];
                            _next = 2;
                            break;
                        case 0x3:
                            V[(_opcode & 0xF00) >> 8] ^= V[(_opcode & 0xF0) >> 4];
                            _next = 2;
                            break;
                        case 0x4:
                            AddRegisters((_opcode & 0xF00) >> 8, (_opcode & 0xF0) >> 4);
                            break;
                        case 0x5:
                            SubRegisters((_opcode & 0xF00) >> 8, (_opcode & 0xF0) >> 4);
                            break;
                        case 0x6:
                            if (InterpreterMode == Chip8InterpreterMode.Schip)
                                ShiftRegisters((_opcode & 0xF00) >> 8);
                            else if (InterpreterMode == Chip8InterpreterMode.Cosmac)
                                ShiftRegistersAlt((_opcode & 0xF00) >> 8, (_opcode & 0xF0) >> 4);
                            break;
                        case 0x7:
                            SubRegisters((_opcode & 0xF00) >> 8, (_opcode & 0xF0) >> 4, true);
                            break;
                        case 0xE:
                            if (InterpreterMode == Chip8InterpreterMode.Schip)
                                ShiftRegisters((_opcode & 0xF00) >> 8, true);
                            else if (InterpreterMode == Chip8InterpreterMode.Cosmac)
                                ShiftRegistersAlt((_opcode & 0xF00) >> 8, (_opcode & 0xF0) >> 4, true);
                            break;
                        default:
                            // Error!
                            throw new IllegalOpcodeException("Illegal opcode called in emulation!", _opcode);
                    }

                    break;
                case 0x9000:
                    SkipInstruction((_opcode & 0xF00) >> 8, V[(_opcode & 0xF0) >> 4], false);
                    break;
                case 0xA000:
                    I = (ushort) (_opcode & 0xFFF);
                    _next = 2;
                    break;
                case 0xB000:
                    JumpToAddress((ushort) ((_opcode & 0xFFF) + V[0]));
                    break;
                case 0xC000:
                    SetRegister((_opcode & 0xF00) >> 8, (byte) (_opcode & 0xFF), true);
                    break;
                case 0xD000:
                    DisplaySprite((_opcode & 0xF00) >> 8, (_opcode & 0xF0) >> 4, _opcode & 0xF);
                    break;
                case 0xE000:
                    switch (_opcode & 0xFF)
                    {
                        case 0x9E:
                            _next = 2;
                            if (Input[V[(_opcode & 0xF00) >> 8]])
                                _next = 4;
                            break;
                        case 0xA1:
                            _next = 2;
                            if (!Input[V[(_opcode & 0xF00) >> 8]])
                                _next = 4;
                            break;
                        default:
                            // Illegal opcode exception
                            throw new IllegalOpcodeException("Illegal opcode called in emulation!", _opcode);
                    }

                    break;
                case 0xF000:
                    var register = (_opcode & 0xF00) >> 8;
                    switch (_opcode & 0xFF)
                    {
                        case 0x07:
                            // Set Vx to timer
                            SetRegister(register, Delay);
                            break;
                        case 0x0A:
                            // Wait for key press, store the value of the key in Vx
                            _next = 0;
                            for (int i = 0; i < 0x10; i++)
                            {
                                if (Input[i])
                                {
                                    _next = 2;
                                }
                            }

                            break;
                        case 0x15:
                            // Set delay timer to Vx
                            Delay = V[register];
                            _next = 2;
                            break;
                        case 0x18:
                            // Set sound timer to Vx
                            Sound = V[register];
                            _next = 2;
                            break;
                        case 0x1E:
                            // Add I and Vx, store in I
                            if (I + V[register] > 0xFFF)
                                V[0xF] = 1;
                            else
                                V[0xF] = 0;
                            I += V[register];
                            _next = 2;
                            break;
                        case 0x29:
                            // Set I to location of hex sprite for fontset
                            I = (ushort) (5 * V[register]);
                            _next = 2;
                            break;
                        case 0x33:
                            // Store BCD of Vx at mem[i], mem[i+1], and mem[i+2]
                            _memory[I] = (byte) (V[register] / 100);
                            _memory[I + 1] = (byte) (V[register] / 10 % 10);
                            _memory[I + 2] = (byte) (V[register] % 10);
                            _next = 2;
                            break;
                        case 0x55:
                            // Store registers V0 - Vx in memory starting at I
                            for (var i = 0; i <= register; i++) _memory[I + i] = V[i];
                            // Interpreter behavior
                            if (InterpreterMode == Chip8InterpreterMode.Cosmac)
                                I += (ushort) (register + 1);

                            _next = 2;
                            break;
                        case 0x65:
                            // Read registers V0 - Vx from memory starting at I
                            for (var i = 0; i <= register; i++) V[i] = _memory[I + i];

                            // Interpreter behavior
                            if (InterpreterMode == Chip8InterpreterMode.Cosmac)
                                I += (ushort) (register + 1);

                            _next = 2;
                            break;
                    }

                    break;
                default:
                    throw new IllegalOpcodeException("Illegal opcode called in emulation!", _opcode);
            }

            PC += _next;
            // Delay
            if (Delay > 0) Delay--;
            if (Sound > 0) Sound--;
        }

        // The part where we implement the opcodes

        #region Opcode Implementation

        /// <summary>
        ///     Zeroes out the display.
        /// </summary>
        private void ClearScreen()
        {
            // Clear display
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 64; x++)
                Display[x, y] = 0;

            Draw = true;
            _next = 2;
        }

        /// <summary>
        ///     Returns from a previously called subroutine. Accesses the stack to set the program counter to the last value, then
        ///     resumes running the program.
        /// </summary>
        private void ReturnFromSubroutine()
        {
            --StackPointer;
            PC = Stack[StackPointer];
            _next = 2;
        }

        /// <summary>
        ///     Sets the program counter to the specified address.
        /// </summary>
        /// <param name="address">The memory address to jump to.</param>
        private void JumpToAddress(ushort address)
        {
            PC = (ushort) (address & 0xFFF);
            _next = 0;
        }

        /// <summary>
        ///     Calls a subroutine. This adds the program counter to the stack and sets it to the address provided.
        /// </summary>
        /// <param name="address">The memory address where the subroutine is located.</param>
        private void CallSubroutine(ushort address)
        {
            Stack[StackPointer] = PC;
            ++StackPointer;
            PC = address;
            _next = 0;
        }

        /// <summary>
        ///     Generic function to determine whether or not to skip an instruction.
        /// </summary>
        /// <param name="register">The register to check.</param>
        /// <param name="value">The value to check against.</param>
        /// <param name="comparator">
        ///     The comparasion to check against. If true, this means that the instruction will be skipped if
        ///     the register's value and the provided value are equal.
        /// </param>
        private void SkipInstruction(int register, byte value, bool comparator)
        {
            if (!((V[register] == value) ^ comparator))
                _next = 4;
            else _next = 2;
        }

        /// <summary>
        ///     Sets the specified register to a value.
        /// </summary>
        /// <param name="register">The register to be set.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="random">
        ///     If true, modifies the behavior of this function to act like opcode CXNN. The register is set to a
        ///     random value and is masked with the value given.
        /// </param>
        private void SetRegister(int register, byte value, bool random = false)
        {
            if (random)
            {
                var rand = new Random();
                var bytes = new byte[1];
                rand.NextBytes(bytes);
                V[register] = (byte) (value & bytes[0]);
                return;
            }

            V[register] = value;
            _next = 2;
        }

        /// <summary>
        ///     Adds a value to the register.
        /// </summary>
        /// <param name="register">The register to be set.</param>
        /// <param name="value">The value to add.</param>
        private void AddRegister(int register, byte value)
        {
            V[register] += value;
            _next = 2;
        }

        /// <summary>
        ///     Adds the values of two registers, and stores the value in Vx. VF is set if a carry occurs, and is unset otherwise.
        /// </summary>
        /// <param name="x">The first register. This register will be set.</param>
        /// <param name="y">The second register.</param>
        private void AddRegisters(int x, int y)
        {
            V[x] = (byte) (V[x] + V[y]);
            if (V[y] > 0xFF - V[x])
                V[0xF] = 1;
            else
                V[0xF] = 0;


            _next = 2;
        }

        /// <summary>
        ///     Subtracts the value of Vy from Vx. VF is set if a borrow occurs, and unset otherwise.
        /// </summary>
        /// <param name="x">The first register. Contains the value that is being subtracted from and is being set.</param>
        /// <param name="y">The second register. Contains the value that will be subtracted from the first register's value.</param>
        /// <param name="useModifiedBehavior">
        ///     If set to true, modifies the behavior to behave as opcode 8XY7. Vx is set to the
        ///     value of Vy - Vx.
        /// </param>
        private void SubRegisters(int x, int y, bool useModifiedBehavior = false)
        {
            var doesBorrow = useModifiedBehavior ? V[x] > V[y] : V[x] < V[y];
            V[0xF] = !doesBorrow ? (byte) 1 : (byte) 0;

            V[x] = useModifiedBehavior ? (byte) (V[y] - V[x]) : (byte) (V[x] - V[y]);
            _next = 2;
        }

        /// <summary>
        ///     Shifts the specified register right. Stores the value of the shifted register in the register, and the least
        ///     significant bit prior is stored in register F.
        ///     This is the SCHIP implementation of this function.
        /// </summary>
        /// <param name="register">The register to shift</param>
        /// <param name="useModifiedBehavior">
        ///     Modifies the behavior. If set to true, shifts the specified register left and stores
        ///     the most siginifant bit prior to the shift in register F.
        /// </param>
        private void ShiftRegisters(int register, bool useModifiedBehavior = false)
        {
            if (useModifiedBehavior)
            {
                V[0xF] = (byte) (V[register] >> 7);
                V[register] = (byte) (V[register] << 1);
            }
            else
            {
                V[0xF] = (byte) (V[register] & 0x1);
                V[register] = (byte) (V[register] >> 1);
            }

            _next = 2;
        }

        /// <summary>
        ///     Shifts the specified register (y) right. Stores the value of the shifted register in register x, and the least
        ///     significant bit prior is stored in register F.
        ///     This is the original (COSMAC) implementation of this function.
        /// </summary>
        /// <param name="x">The register that will be set.</param>
        /// <param name="y">The register to be shifted.</param>
        /// <param name="useModifiedBehavior">
        ///     Modifies the behavior. If set to true, shifts the specified register left and stores
        ///     the most siginifant bit prior to the shift in register F.
        /// </param>
        private void ShiftRegistersAlt(int x, int y, bool useModifiedBehavior = false)
        {
            if (useModifiedBehavior)
            {
                V[0xF] = (byte) (V[y] >> 7);
                V[x] = (byte) (V[y] << 1);
            }
            else
            {
                V[0xF] = (byte) (V[y] & 0x1);
                V[x] = (byte) (V[y] >> 1);
            }

            _next = 2;
        }

        /// <summary>
        ///     Displays a sprite with coordinates Vx and Vy with N bytes of data located at the address stored in I. VF is set if
        ///     any set pixels are unset, and unset otherwise.
        /// </summary>
        /// <param name="x">The register containing the x coordinate.</param>
        /// <param name="y">The register containing the y coordinate.</param>
        /// <param name="n">
        ///     The number of bytes to read for the sprite. Each byte corresponds to an 8-pixel row of sprites, and
        ///     each row is drawn top to bottom.
        /// </param>
        private void DisplaySprite(int x, int y, int n)
        {
            V[0xF] = 0;
            x = V[x];
            y = V[y];

            for (var rY = 0; rY < n; rY++)
            {
                ushort pixel = _memory[I + rY];
                for (var rX = 0; rX < 8; rX++)
                    if ((pixel & (0x80 >> rX)) != 0)
                    {
                        if (Display[(x + rX) % 64, (y + rY) % 32] == 1)
                            V[0xF] = 1;
                        Display[(x + rX) % 64, (y + rY) % 32] ^= 1;
                    }
            }

            Draw = true;
            _next = 2;
        }

        #endregion
    }

    public enum Chip8WrappingMode
    {
        Wrap,
        DoNotDraw
    }

    public enum Chip8InterpreterMode
    {
        Cosmac,
        Schip
    }

    public class IllegalOpcodeException : Exception
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public ushort Opcode { get; }

        public IllegalOpcodeException()
        {
        }

        public IllegalOpcodeException(string message) : base(message)
        {
        }

        public IllegalOpcodeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public IllegalOpcodeException(string message, ushort opcode) : base(message)
        {
            Opcode = opcode;
        }
    }
}
﻿using System.Diagnostics.CodeAnalysis;

namespace WasmDisassembler;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum WasmMnemonic : byte
{
    Unreachable = 0x0,
    Nop,
    Block,
    Loop,
    If,
    Else,
    Proposed_Try,
    Proposed_Catch,
    Proposed_Throw,
    Proposed_Rethrow,
    Proposed_BrOnExn,
    End,
    Br,
    BrIf,
    BrTable,
    Return,
    Call, //0x10
    CallIndirect,
    Proposed_ReturnCall,
    Proposed_ReturnCallIndirect, //0x13. Reserved block start
    Reserved_14,
    Reserved_15,
    Reserved_16,
    Reserved_17,
    Reserved_18,
    Reserved_19,
    Drop,
    Select,
    Proposed_SelectT,
    Reserved_1D,
    Reserved_1E,
    Reserved_1F,
    LocalGet, //0x20
    LocalSet,
    LocalTee,
    GlobalGet,
    GlobalSet,
    Proposed_TableGet,
    Proposed_TableSet,
    Reserved_27,
    I32Load,
    I64Load,
    F32Load,
    F64Load,
    I32Load8_S,
    I32Load8_U,
    I32Load16_S,
    I32Load16_U,
    I64Load8_S, //0x30
    I64Load8_U,
    I64Load16_S,
    I64Load16_U,
    I64Load32_S,
    I64Load32_U,
    I32Store,
    I64Store,
    F32Store,
    F64Store,
    I32Store8,
    I32Store16,
    I64Store8,
    I64Store16,
    I64Store32,
    MemorySize,
    MemoryGrow, //0x40
    I32Const,
    I64Const,
    F32Const,
    F64Const,
    I32Eqz,
    I32Eq,
    I32Ne,
    I32Lt_S,
    I32Lt_U,
    I32Gt_S,
    I32Gt_U,
    I32Le_S,
    I32Le_U,
    I32Ge_S,
    I32Ge_U,
    I64Eqz, //0x50
    I64Eq,
    I64Ne,
    I64Lt_S,
    I64Lt_U,
    I64Gt_S,
    I64Gt_U,
    I64Le_S,
    I64Le_U,
    I64Ge_S,
    I64Ge_U,
    F32Eq,
    F32Ne,
    F32Lt,
    F32Gt,
    F32Le,
    F32Ge, //0x60
    F64Eq,
    F64Ne,
    F64Lt,
    F64Gt,
    F64Le,
    F64Ge,
    I32Clz,
    I32Ctz,
    I32PopCnt,
    I32Add,
    I32Sub,
    I32Mul,
    I32Div_S,
    I32Div_U,
    I32Rem_S,
    I32Rem_U, //0x70
    I32And,
    I32Or,
    I32Xor,
    I32Shl,
    I32Shr_S,
    I32Shr_U,
    I32Rotl,
    I32Rotr,
    I64Clz,
    I64Ctz,
    I64PopCnt,
    I64Add,
    I64Sub,
    I64Mul,
    I64Div_S,
    I64Div_U, //0x80
    I64Rem_S,
    I64Rem_U,
    I64And,
    I64Or,
    I64Xor,
    I64Shl,
    I64Shr_S,
    I64Shr_U,
    I64Rotl,
    I64Rotr,
    F32Abs,
    F32Neg,
    F32Ceil,
    F32Floor,
    F32Trunc,
    F32Nearest, //0x90
    F32Sqrt,
    F32Add,
    F32Sub,
    F32Mul,
    F32Div,
    F32Min,
    F32Max,
    F32Copysign,
    F64Abs,
    F64Neg,
    F64Ceil,
    F64Floor,
    F64Trunc,
    F64Nearest,
    F64Sqrt,
    F64Add, //0xA0
    F64Sub,
    F64Mul,
    F64Div,
    F64Min,
    F64Max,
    F64Copysign,
    I32Wrap_I64,
    I32Trunc_F32_S,
    I32Trunc_F32_U,
    I32Trunc_F64_S,
    I32Trunc_F64_U,
    I64Extend_I32_S,
    I64Extend_I32_U,
    I64Trunc_F32_S,
    I64Trunc_F32_U,
    I64Trunc_F64_S, //0xB0
    I64Trunc_F64_U,
    F32Convert_I32_S,
    F32Convert_I32_U,
    F32Convert_I64_S,
    F32Convert_I64_U,
    F32Demote_F64,
    F64Convert_I32_S,
    F64Convert_I32_U,
    F64Convert_I64_S,
    F64Convert_I64_U,
    F64Promote_F32,
    I32Reinterpret_F32,
    I64Reinterpret_F64,
    F32Reinterpret_I32,
    F64Reinterpret_I64,
    LastValid = F64Reinterpret_I64,

    Proposed_I32Extend8_S, //0xC0
    Proposed_I32Extend16_S,
    Proposed_I64Extend8_S,
    Proposed_I64Extend16_S,
    Proposed_I64Extend32_S,

    //Reserved: 0xC5-CF
    Proposed_RefNull = 0xD0,
    Proposed_RefIsNull,
    Proposed_RefFunc,

    //Reserved: 0xD3-FB,
    Proposed_FC_Extensions = 0xFC,
    Proposed_SIMD,
    //Reserved: 0xFE-FF
}

#pragma once
namespace ArisenEngine::RHI
{
    typedef enum ECompareOp {
        COMPARE_OP_NEVER = 0,
        COMPARE_OP_LESS = 1,
        COMPARE_OP_EQUAL = 2,
        COMPARE_OP_LESS_OR_EQUAL = 3,
        COMPARE_OP_GREATER = 4,
        COMPARE_OP_NOT_EQUAL = 5,
        COMPARE_OP_GREATER_OR_EQUAL = 6,
        COMPARE_OP_ALWAYS = 7,
        COMPARE_OP_MAX_ENUM = 0x7FFFFFFF
    } ECompareOp;
}

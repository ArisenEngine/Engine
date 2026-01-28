#pragma once

#include <string>
#include <vector>
#include <string_view>
#include <utility>
#include "CoreFoundationCommon.h"

namespace ArisenEngine::Infra
{
    /**
     * @brief custom String class for the engine.
     * Uses UTF-8 as internal storage (std::string).
     * Provides seamless compatibility with std::wstring and C-style strings.
     */
    class FOUNDATION_DLL String
    {
    public:
        String() = default;
        String(const char* str);
        String(const wchar_t* wstr);
        String(const std::string& str);
        String(const std::wstring& wstr);
        String(const String& other) = default;
        String(String&& other) noexcept = default;

        ~String() = default;

        String& operator=(const char* str);
        String& operator=(const wchar_t* wstr);
        String& operator=(const std::string& str);
        String& operator=(const std::wstring& wstr);
        String& operator=(const String& other) = default;
        String& operator=(String&& other) noexcept = default;

        // Operators
        bool operator==(const String& other) const { return m_Data == other.m_Data; }
        bool operator!=(const String& other) const { return m_Data != other.m_Data; }
        bool operator<(const String& other) const { return m_Data < other.m_Data; }
        
        String operator+(const String& other) const;
        String& operator+=(const String& other);

        // Accessors
        const char* c_str() const { return m_Data.c_str(); }
        const std::string& GetString() const { return m_Data; }
        std::wstring ToWString() const;

        size_t Length() const { return m_Data.length(); }
        bool IsEmpty() const { return m_Data.empty(); }
        void Clear() { m_Data.clear(); }

        // Implicit conversions
        operator const std::string&() const { return m_Data; }
        operator std::string_view() const { return m_Data; }

        // Utility Methods
        static String Format(const char* format, ...);
        std::vector<String> Split(char delimiter) const;
        String Trim() const;
        bool Contains(const String& substr) const;
        bool StartsWith(const String& prefix) const;
        bool EndsWith(const String& suffix) const;

        static std::string WStringToString(const std::wstring& wstr);
        static std::wstring StringToWString(const std::string& str);

    private:
        std::string m_Data;
    };

    // Global operators for flexibility
    inline String operator+(const String& lhs, const char* rhs) { return lhs + String(rhs); }
    inline String operator+(const char* lhs, const String& rhs) { return String(lhs) + rhs; }
    inline String operator+(const String& lhs, const wchar_t* rhs) { return lhs + String(rhs); }
    inline String operator+(const wchar_t* lhs, const String& rhs) { return String(lhs) + rhs; }
}

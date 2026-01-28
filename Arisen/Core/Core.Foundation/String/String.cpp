#include "String.h"
#include <cstdarg>
#include <cwchar>
#include <algorithm>

namespace ArisenEngine
{
    String::String(const char* str) : m_Data(str ? str : "") {}

    String::String(const wchar_t* wstr) : m_Data(WStringToString(wstr ? wstr : L"")) {}

    String::String(const std::string& str) : m_Data(str) {}

    String::String(const std::wstring& wstr) : m_Data(WStringToString(wstr)) {}

    String& String::operator=(const char* str)
    {
        m_Data = str ? str : "";
        return *this;
    }

    String& String::operator=(const wchar_t* wstr)
    {
        m_Data = WStringToString(wstr ? wstr : L"");
        return *this;
    }

    String& String::operator=(const std::string& str)
    {
        m_Data = str;
        return *this;
    }

    String& String::operator=(const std::wstring& wstr)
    {
        m_Data = WStringToString(wstr);
        return *this;
    }

    String String::operator+(const String& other) const
    {
        return String(m_Data + other.m_Data);
    }

    String& String::operator+=(const String& other)
    {
        m_Data += other.m_Data;
        return *this;
    }

    std::wstring String::ToWString() const
    {
        return StringToWString(m_Data);
    }

    std::string String::WStringToString(const std::wstring& wstr)
    {
        if (wstr.empty()) return "";
        
        std::mbstate_t state = std::mbstate_t();
        const wchar_t* src = wstr.data();
        size_t len = 0;
        wcsrtombs_s(&len, nullptr, 0, &src, 0, &state);

        if (len == 0 || len == static_cast<size_t>(-1)) return "";

        std::vector<char> dst(len);
        wcsrtombs_s(&len, dst.data(), dst.size(), &src, dst.size(), &state);
        
        // Remove null terminator if it was included in len
        if (!dst.empty() && dst.back() == '\0') {
            dst.pop_back();
        }
        
        return std::string(dst.begin(), dst.end());
    }

    std::wstring String::StringToWString(const std::string& str)
    {
        if (str.empty()) return L"";

        std::mbstate_t state = std::mbstate_t();
        const char* src = str.data();
        size_t len = 0;
        mbsrtowcs_s(&len, nullptr, 0, &src, 0, &state);

        if (len == 0 || len == static_cast<size_t>(-1)) return L"";

        std::vector<wchar_t> dst(len);
        mbsrtowcs_s(&len, dst.data(), dst.size(), &src, dst.size(), &state);

        if (!dst.empty() && dst.back() == L'\0') {
            dst.pop_back();
        }

        return std::wstring(dst.begin(), dst.end());
    }

    String String::Format(const char* format, ...)
    {
        char buffer[1024]; // Basic buffer
        va_list args;
        va_start(args, format);
        int result = vsnprintf(buffer, sizeof(buffer), format, args);
        va_end(args);

        if (result >= 0 && result < static_cast<int>(sizeof(buffer)))
        {
            return String(buffer);
        }
        else if (result >= static_cast<int>(sizeof(buffer)))
        {
            // If buffer was too small, allocate dynamically
            std::vector<char> dynamicBuffer(result + 1);
            va_start(args, format);
            vsnprintf(dynamicBuffer.data(), dynamicBuffer.size(), format, args);
            va_end(args);
            return String(dynamicBuffer.data());
        }
        return String("");
    }

    std::vector<String> String::Split(char delimiter) const
    {
        std::vector<String> result;
        size_t start = 0;
        size_t end = m_Data.find(delimiter);

        while (end != std::string::npos)
        {
            result.push_back(m_Data.substr(start, end - start));
            start = end + 1;
            end = m_Data.find(delimiter, start);
        }
        result.push_back(m_Data.substr(start));
        return result;
    }

    String String::Trim() const
    {
        std::string s = m_Data;
        s.erase(s.begin(), std::find_if(s.begin(), s.end(), [](unsigned char ch) {
            return !std::isspace(ch);
        }));
        s.erase(std::find_if(s.rbegin(), s.rend(), [](unsigned char ch) {
            return !std::isspace(ch);
        }).base(), s.end());
        return String(s);
    }

    bool String::Contains(const String& substr) const
    {
        return m_Data.find(substr.m_Data) != std::string::npos;
    }

    bool String::StartsWith(const String& prefix) const
    {
        if (prefix.Length() > Length()) return false;
        return m_Data.compare(0, prefix.Length(), prefix.m_Data) == 0;
    }

    bool String::EndsWith(const String& suffix) const
    {
        if (suffix.Length() > Length()) return false;
        return m_Data.compare(Length() - suffix.Length(), suffix.Length(), suffix.m_Data) == 0;
    }
}

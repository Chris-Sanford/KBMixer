//! Small string helpers matching .NET's OrdinalIgnoreCase and System.IO.Path semantics.

use std::cmp::Ordering;

fn fold(c: char) -> impl Iterator<Item = char> {
    c.to_uppercase()
}

/// `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)`
pub fn eq_ic(a: &str, b: &str) -> bool {
    if a.len() == b.len() && a.eq_ignore_ascii_case(b) {
        return true;
    }
    a.chars().flat_map(fold).eq(b.chars().flat_map(fold))
}

/// `string.Compare(a, b, StringComparison.OrdinalIgnoreCase)`
pub fn cmp_ic(a: &str, b: &str) -> Ordering {
    a.chars().flat_map(fold).cmp(b.chars().flat_map(fold))
}

/// `haystack.Contains(needle, StringComparison.OrdinalIgnoreCase)`
pub fn contains_ic(haystack: &str, needle: &str) -> bool {
    if needle.is_empty() {
        return true;
    }
    haystack.to_uppercase().contains(&needle.to_uppercase())
}

/// `Path.GetFileName`
pub fn file_name(path: &str) -> &str {
    match path.rfind(['\\', '/', ':']) {
        Some(i) => &path[i + 1..],
        None => path,
    }
}

/// `Path.GetFileNameWithoutExtension`
pub fn file_stem(path: &str) -> &str {
    let name = file_name(path);
    match name.rfind('.') {
        Some(i) => &name[..i],
        None => name,
    }
}

pub fn is_blank(s: &str) -> bool {
    s.trim().is_empty()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn paths() {
        assert_eq!(file_name(r"C:\a\b\chrome.exe"), "chrome.exe");
        assert_eq!(file_stem(r"C:\a\b\Slippi Dolphin.exe"), "Slippi Dolphin");
        assert_eq!(file_stem("cod"), "cod");
    }

    #[test]
    fn case_insensitive() {
        assert!(eq_ic("Discord.EXE", "discord.exe"));
        assert!(contains_ic("Discord (Instance 2)", "discord"));
        assert_eq!(cmp_ic("apple", "Banana"), Ordering::Less);
    }
}

//! Minimal JSON: a parser into `Value`, and an indented writer that matches System.Text.Json's layout.

#[derive(Clone, Debug, PartialEq)]
pub enum Value {
    Null,
    Bool(bool),
    Number(f64),
    String(String),
    Array(Vec<Value>),
    Object(Vec<(String, Value)>),
}

impl Value {
    pub fn get(&self, key: &str) -> Option<&Value> {
        match self {
            Value::Object(m) => m.iter().find(|(k, _)| k == key).map(|(_, v)| v),
            _ => None,
        }
    }
    pub fn as_str(&self) -> Option<&str> {
        match self {
            Value::String(s) => Some(s),
            _ => None,
        }
    }
    pub fn as_array(&self) -> Option<&[Value]> {
        match self {
            Value::Array(a) => Some(a),
            _ => None,
        }
    }
}

pub fn parse(src: &str) -> Result<Value, String> {
    let mut p = Parser { s: src.as_bytes(), i: 0 };
    // Tolerate a UTF-8 BOM.
    if p.s.starts_with(&[0xEF, 0xBB, 0xBF]) {
        p.i = 3;
    }
    let v = p.value(0)?;
    p.ws();
    if p.i != p.s.len() {
        return Err(format!("unexpected trailing data at byte {}", p.i));
    }
    Ok(v)
}

struct Parser<'a> {
    s: &'a [u8],
    i: usize,
}

impl Parser<'_> {
    fn ws(&mut self) {
        while self.i < self.s.len() && matches!(self.s[self.i], b' ' | b'\t' | b'\n' | b'\r') {
            self.i += 1;
        }
    }

    fn err<T>(&self, what: &str) -> Result<T, String> {
        Err(format!("{what} at byte {}", self.i))
    }

    fn value(&mut self, depth: u32) -> Result<Value, String> {
        if depth > 64 {
            return self.err("nesting too deep");
        }
        self.ws();
        let Some(&c) = self.s.get(self.i) else { return self.err("unexpected end") };
        match c {
            b'{' => {
                self.i += 1;
                let mut m = Vec::new();
                self.ws();
                if self.s.get(self.i) == Some(&b'}') {
                    self.i += 1;
                    return Ok(Value::Object(m));
                }
                loop {
                    self.ws();
                    if self.s.get(self.i) != Some(&b'"') {
                        return self.err("expected key");
                    }
                    let k = self.string()?;
                    self.ws();
                    if self.s.get(self.i) != Some(&b':') {
                        return self.err("expected ':'");
                    }
                    self.i += 1;
                    let v = self.value(depth + 1)?;
                    m.push((k, v));
                    self.ws();
                    match self.s.get(self.i) {
                        Some(b',') => self.i += 1,
                        Some(b'}') => {
                            self.i += 1;
                            return Ok(Value::Object(m));
                        }
                        _ => return self.err("expected ',' or '}'"),
                    }
                }
            }
            b'[' => {
                self.i += 1;
                let mut a = Vec::new();
                self.ws();
                if self.s.get(self.i) == Some(&b']') {
                    self.i += 1;
                    return Ok(Value::Array(a));
                }
                loop {
                    a.push(self.value(depth + 1)?);
                    self.ws();
                    match self.s.get(self.i) {
                        Some(b',') => self.i += 1,
                        Some(b']') => {
                            self.i += 1;
                            return Ok(Value::Array(a));
                        }
                        _ => return self.err("expected ',' or ']'"),
                    }
                }
            }
            b'"' => Ok(Value::String(self.string()?)),
            b't' => self.lit("true", Value::Bool(true)),
            b'f' => self.lit("false", Value::Bool(false)),
            b'n' => self.lit("null", Value::Null),
            b'-' | b'0'..=b'9' => self.number(),
            _ => self.err("unexpected character"),
        }
    }

    fn lit(&mut self, word: &str, v: Value) -> Result<Value, String> {
        if self.s[self.i..].starts_with(word.as_bytes()) {
            self.i += word.len();
            Ok(v)
        } else {
            self.err("invalid literal")
        }
    }

    fn number(&mut self) -> Result<Value, String> {
        let start = self.i;
        while self.i < self.s.len() && matches!(self.s[self.i], b'-' | b'+' | b'.' | b'e' | b'E' | b'0'..=b'9') {
            self.i += 1;
        }
        let t = std::str::from_utf8(&self.s[start..self.i]).unwrap_or("");
        t.parse::<f64>().map(Value::Number).or_else(|_| self.err("invalid number"))
    }

    fn hex4(&mut self) -> Result<u16, String> {
        let Some(h) = self.s.get(self.i..self.i + 4) else { return self.err("bad escape") };
        let t = std::str::from_utf8(h).map_err(|e| e.to_string())?;
        let v = u16::from_str_radix(t, 16).or_else(|_| self.err("bad \\u escape"))?;
        self.i += 4;
        Ok(v)
    }

    fn string(&mut self) -> Result<String, String> {
        self.i += 1; // opening quote
        let mut out = String::new();
        loop {
            let Some(&c) = self.s.get(self.i) else { return self.err("unterminated string") };
            match c {
                b'"' => {
                    self.i += 1;
                    return Ok(out);
                }
                b'\\' => {
                    self.i += 1;
                    let Some(&e) = self.s.get(self.i) else { return self.err("bad escape") };
                    self.i += 1;
                    match e {
                        b'"' => out.push('"'),
                        b'\\' => out.push('\\'),
                        b'/' => out.push('/'),
                        b'b' => out.push('\u{8}'),
                        b'f' => out.push('\u{c}'),
                        b'n' => out.push('\n'),
                        b'r' => out.push('\r'),
                        b't' => out.push('\t'),
                        b'u' => {
                            let hi = self.hex4()?;
                            let mut units = vec![hi];
                            if (0xD800..0xDC00).contains(&hi) && self.s[self.i..].starts_with(b"\\u") {
                                self.i += 2;
                                units.push(self.hex4()?);
                            }
                            out.push_str(&String::from_utf16_lossy(&units));
                        }
                        _ => return self.err("bad escape"),
                    }
                }
                _ => {
                    // Copy a run of plain bytes (valid UTF-8 because the input is &str).
                    let start = self.i;
                    while self.i < self.s.len() && self.s[self.i] != b'"' && self.s[self.i] != b'\\' {
                        self.i += 1;
                    }
                    out.push_str(std::str::from_utf8(&self.s[start..self.i]).unwrap_or(""));
                }
            }
        }
    }
}

/// Escapes like System.Text.Json's default encoder: non-ASCII and HTML-sensitive characters become \uXXXX.
pub fn write_string(out: &mut String, s: &str) {
    out.push('"');
    for ch in s.chars() {
        match ch {
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            '\u{8}' => out.push_str("\\b"),
            '\u{c}' => out.push_str("\\f"),
            '"' | '&' | '\'' | '<' | '>' | '+' | '`' => push_u(out, ch as u16),
            c if (c as u32) < 0x20 || (c as u32) > 0x7E => {
                let mut buf = [0u16; 2];
                for unit in c.encode_utf16(&mut buf) {
                    push_u(out, *unit);
                }
            }
            c => out.push(c),
        }
    }
    out.push('"');
}

fn push_u(out: &mut String, unit: u16) {
    out.push_str(&format!("\\u{unit:04X}"));
}

/// Builder for `{ "Key": value, ... }` with 2-space indentation (WriteIndented = true).
pub struct ObjectWriter {
    out: String,
    first: bool,
}

impl Default for ObjectWriter {
    fn default() -> Self {
        Self::new()
    }
}

impl ObjectWriter {
    pub fn new() -> Self {
        ObjectWriter { out: String::from("{"), first: true }
    }
    fn key(&mut self, k: &str) {
        if !self.first {
            self.out.push(',');
        }
        self.first = false;
        self.out.push_str("\n  ");
        write_string(&mut self.out, k);
        self.out.push_str(": ");
    }
    pub fn string(&mut self, k: &str, v: &str) {
        self.key(k);
        write_string(&mut self.out, v);
    }
    pub fn opt_string(&mut self, k: &str, v: Option<&str>) {
        match v {
            Some(v) => self.string(k, v),
            None => {
                self.key(k);
                self.out.push_str("null");
            }
        }
    }
    pub fn bool(&mut self, k: &str, v: bool) {
        self.key(k);
        self.out.push_str(if v { "true" } else { "false" });
    }
    pub fn int(&mut self, k: &str, v: i64) {
        self.key(k);
        self.out.push_str(&v.to_string());
    }
    pub fn int_array(&mut self, k: &str, v: &[i32]) {
        self.key(k);
        if v.is_empty() {
            self.out.push_str("[]");
            return;
        }
        self.out.push('[');
        for (i, n) in v.iter().enumerate() {
            if i > 0 {
                self.out.push(',');
            }
            self.out.push_str("\n    ");
            self.out.push_str(&n.to_string());
        }
        self.out.push_str("\n  ]");
    }
    pub fn finish(mut self) -> String {
        self.out.push_str(if self.first { "}" } else { "\n}" });
        self.out
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trip_strings() {
        let mut s = String::new();
        write_string(&mut s, "Café \"x\" + <y>");
        let v = parse(&s).unwrap();
        assert_eq!(v.as_str(), Some("Café \"x\" + <y>"));
        assert!(s.is_ascii());
    }

    #[test]
    fn parses_objects() {
        let v = parse("{\"a\": [1, 2], \"b\": null, \"c\": true, \"d\": \"\\u00e9\\ud83d\\ude00\"}").unwrap();
        assert_eq!(v.get("a").unwrap().as_array().unwrap().len(), 2);
        assert_eq!(v.get("b"), Some(&Value::Null));
        assert_eq!(v.get("d").unwrap().as_str(), Some("é😀"));
        assert!(parse("{\"a\": }").is_err());
    }
}

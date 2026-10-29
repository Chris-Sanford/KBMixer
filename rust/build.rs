//! Compiles the icon, version info and manifest into a .res with the Windows SDK's rc.exe and links it.
//! Uses std only so the crate keeps zero dependencies.

use std::env;
use std::path::{Path, PathBuf};
use std::process::Command;

fn main() {
    println!("cargo:rerun-if-changed=res/kbmixer.manifest");
    println!("cargo:rerun-if-changed=res/KBMixer.ico");
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-env-changed=KBMIXER_RC");

    if env::var("CARGO_CFG_TARGET_OS").as_deref() != Ok("windows") {
        return;
    }

    let out_dir = PathBuf::from(env::var("OUT_DIR").unwrap());
    let manifest_dir = PathBuf::from(env::var("CARGO_MANIFEST_DIR").unwrap());
    let Some(rc) = find_rc() else {
        println!("cargo:warning=rc.exe not found (install the Windows SDK or set KBMIXER_RC); building without icon/manifest");
        return;
    };

    let version = env::var("CARGO_PKG_VERSION").unwrap();
    let mut nums: Vec<&str> = version.split(['.', '-']).take(3).collect();
    while nums.len() < 3 {
        nums.push("0");
    }
    let comma = format!("{},{},{},0", nums[0], nums[1], nums[2]);
    let dotted = format!("{}.{}.{}.0", nums[0], nums[1], nums[2]);
    let esc = |p: PathBuf| p.display().to_string().replace(std::path::MAIN_SEPARATOR, "/");
    let ico = esc(manifest_dir.join("res").join("KBMixer.ico"));
    let manifest = esc(manifest_dir.join("res").join("kbmixer.manifest"));

    // Numeric constants instead of <winver.h> so rc.exe needs no include paths.
    let script = format!(
        r#"1 ICON "{ico}"
1 24 "{manifest}"
1 VERSIONINFO
FILEVERSION {comma}
PRODUCTVERSION {comma}
FILEFLAGSMASK 0x3fL
FILEFLAGS 0x0L
FILEOS 0x40004L
FILETYPE 0x1L
FILESUBTYPE 0x0L
BEGIN
    BLOCK "StringFileInfo"
    BEGIN
        BLOCK "040904b0"
        BEGIN
            VALUE "CompanyName", "Sanford Technologies LLC"
            VALUE "FileDescription", "Keyboard and Mouse Audio Mixer - Control application volume with hotkeys and mouse wheel"
            VALUE "FileVersion", "{dotted}"
            VALUE "InternalName", "KBMixer"
            VALUE "LegalCopyright", "Copyright \251 2025"
            VALUE "OriginalFilename", "KBMixer.exe"
            VALUE "ProductName", "KBMixer"
            VALUE "ProductVersion", "{dotted}"
        END
    END
    BLOCK "VarFileInfo"
    BEGIN
        VALUE "Translation", 0x409, 1200
    END
END
"#
    );
    let rc_path = out_dir.join("kbmixer.rc");
    std::fs::write(&rc_path, script).unwrap();
    let res = out_dir.join("kbmixer.res");

    let status = Command::new(&rc)
        .arg("/nologo")
        .arg("/fo")
        .arg(&res)
        .arg(&rc_path)
        .status()
        .expect("failed to run rc.exe");
    assert!(status.success(), "rc.exe failed");

    if env::var("CARGO_CFG_TARGET_ENV").as_deref() == Ok("msvc") {
        // link.exe / lld-link convert .res themselves.
        println!("cargo:rustc-link-arg-bins={}", res.display());
    } else {
        // GNU ld only takes COFF objects: do cvtres's job here.
        let obj = out_dir.join("kbmixer_res.o");
        std::fs::write(&obj, res_to_coff(&std::fs::read(&res).unwrap())).unwrap();
        println!("cargo:rustc-link-arg-bins={}", obj.display());
    }
}

struct ResEntry {
    ty: u16,
    name: u16,
    lang: u16,
    data: Vec<u8>,
}

fn rd16(b: &[u8], o: usize) -> u16 {
    u16::from_le_bytes([b[o], b[o + 1]])
}
fn rd32(b: &[u8], o: usize) -> u32 {
    u32::from_le_bytes([b[o], b[o + 1], b[o + 2], b[o + 3]])
}

/// Parses a 32-bit .res file (numeric type and name ids only, which is all this crate emits).
fn parse_res(b: &[u8]) -> Vec<ResEntry> {
    let mut out = Vec::new();
    let mut o = 0;
    while o + 32 <= b.len() {
        let data_size = rd32(b, o) as usize;
        let header_size = rd32(b, o + 4) as usize;
        let mut p = o + 8;
        let mut id = || {
            assert_eq!(rd16(b, p), 0xFFFF, "named resources are not supported");
            let v = rd16(b, p + 2);
            p += 4;
            v
        };
        let ty = id();
        let name = id();
        // DataVersion(4) MemoryFlags(2) LanguageId(2)
        let lang = rd16(b, p + 6);
        let data_start = o + header_size;
        if !(data_size == 0 && ty == 0) {
            out.push(ResEntry { ty, name, lang, data: b[data_start..data_start + data_size].to_vec() });
        }
        o = (data_start + data_size + 3) & !3;
    }
    out
}

/// Builds an AMD64 COFF object with one .rsrc section (directory tree + data, ADDR32NB relocations).
fn res_to_coff(res: &[u8]) -> Vec<u8> {
    let mut entries = parse_res(res);
    entries.sort_by_key(|e| (e.ty, e.name, e.lang));

    let mut types: Vec<u16> = entries.iter().map(|e| e.ty).collect();
    types.dedup();
    let names_of = |t: u16| {
        let mut v: Vec<u16> = entries.iter().filter(|e| e.ty == t).map(|e| e.name).collect();
        v.dedup();
        v
    };

    // Directory sizes: 16-byte header + 8 per entry.
    let dir = |n: usize| 16 + 8 * n;
    let mut size = dir(types.len());
    let mut type_off = Vec::new();
    for &t in &types {
        type_off.push(size);
        size += dir(names_of(t).len());
    }
    let mut name_off = Vec::new(); // (ty, name, offset)
    for &t in &types {
        for n in names_of(t) {
            name_off.push((t, n, size));
            size += dir(entries.iter().filter(|e| e.ty == t && e.name == n).count());
        }
    }
    let data_entries_off = size;
    size += 16 * entries.len();
    let mut blob_off = Vec::new();
    for e in &entries {
        size = (size + 7) & !7;
        blob_off.push(size);
        size += e.data.len();
    }

    let mut sec = vec![0u8; size];
    let put16 = |s: &mut Vec<u8>, o: usize, v: u16| s[o..o + 2].copy_from_slice(&v.to_le_bytes());
    let put32 = |s: &mut Vec<u8>, o: usize, v: u32| s[o..o + 4].copy_from_slice(&v.to_le_bytes());
    let header = |s: &mut Vec<u8>, o: usize, n: usize| put16(s, o + 14, n as u16);

    header(&mut sec, 0, types.len());
    for (i, &t) in types.iter().enumerate() {
        put32(&mut sec, 16 + 8 * i, t as u32);
        put32(&mut sec, 16 + 8 * i + 4, 0x8000_0000 | type_off[i] as u32);
        let names = names_of(t);
        header(&mut sec, type_off[i], names.len());
        for (j, &n) in names.iter().enumerate() {
            let off = name_off.iter().find(|x| x.0 == t && x.1 == n).unwrap().2;
            put32(&mut sec, type_off[i] + 16 + 8 * j, n as u32);
            put32(&mut sec, type_off[i] + 16 + 8 * j + 4, 0x8000_0000 | off as u32);
        }
    }
    let mut relocs = Vec::new();
    for &(t, n, off) in &name_off {
        let langs: Vec<usize> = (0..entries.len()).filter(|&k| entries[k].ty == t && entries[k].name == n).collect();
        header(&mut sec, off, langs.len());
        for (j, &k) in langs.iter().enumerate() {
            put32(&mut sec, off + 16 + 8 * j, entries[k].lang as u32);
            put32(&mut sec, off + 16 + 8 * j + 4, (data_entries_off + 16 * k) as u32);
        }
    }
    for (k, e) in entries.iter().enumerate() {
        let de = data_entries_off + 16 * k;
        put32(&mut sec, de, blob_off[k] as u32); // section offset; the linker adds the section RVA
        put32(&mut sec, de + 4, e.data.len() as u32);
        relocs.push(de as u32);
        sec[blob_off[k]..blob_off[k] + e.data.len()].copy_from_slice(&e.data);
    }

    // COFF: file header (20) + section header (40) + raw data + relocations (10 each) + symbols + string table.
    let raw_ptr = 20 + 40;
    let reloc_ptr = raw_ptr + sec.len();
    let sym_ptr = reloc_ptr + 10 * relocs.len();
    let mut o = Vec::new();
    o.extend_from_slice(&0x8664u16.to_le_bytes());
    o.extend_from_slice(&1u16.to_le_bytes());
    o.extend_from_slice(&0u32.to_le_bytes());
    o.extend_from_slice(&(sym_ptr as u32).to_le_bytes());
    o.extend_from_slice(&1u32.to_le_bytes());
    o.extend_from_slice(&0u16.to_le_bytes());
    o.extend_from_slice(&0u16.to_le_bytes());

    o.extend_from_slice(b".rsrc\0\0\0");
    o.extend_from_slice(&0u32.to_le_bytes()); // VirtualSize
    o.extend_from_slice(&0u32.to_le_bytes()); // VirtualAddress
    o.extend_from_slice(&(sec.len() as u32).to_le_bytes());
    o.extend_from_slice(&(raw_ptr as u32).to_le_bytes());
    o.extend_from_slice(&(reloc_ptr as u32).to_le_bytes());
    o.extend_from_slice(&0u32.to_le_bytes()); // line numbers
    o.extend_from_slice(&(relocs.len() as u16).to_le_bytes());
    o.extend_from_slice(&0u16.to_le_bytes());
    // INITIALIZED_DATA | ALIGN_4BYTES | MEM_READ
    o.extend_from_slice(&(0x0000_0040u32 | 0x0030_0000 | 0x4000_0000).to_le_bytes());

    o.extend_from_slice(&sec);
    for r in relocs {
        o.extend_from_slice(&r.to_le_bytes());
        o.extend_from_slice(&0u32.to_le_bytes()); // symbol 0 (.rsrc)
        o.extend_from_slice(&3u16.to_le_bytes()); // IMAGE_REL_AMD64_ADDR32NB
    }
    o.extend_from_slice(b".rsrc\0\0\0");
    o.extend_from_slice(&0u32.to_le_bytes()); // value
    o.extend_from_slice(&1u16.to_le_bytes()); // section number
    o.extend_from_slice(&0u16.to_le_bytes()); // type
    o.push(3); // IMAGE_SYM_CLASS_STATIC
    o.push(0); // aux
    o.extend_from_slice(&4u32.to_le_bytes()); // empty string table
    o
}

fn find_rc() -> Option<PathBuf> {
    if let Ok(p) = env::var("KBMIXER_RC") {
        return Some(PathBuf::from(p));
    }
    let arch = match env::var("CARGO_CFG_TARGET_ARCH").as_deref() {
        Ok("aarch64") => "arm64",
        Ok("x86") => "x86",
        _ => "x64",
    };
    for var in ["ProgramFiles(x86)", "ProgramFiles"] {
        let Ok(pf) = env::var(var) else { continue };
        let root = Path::new(&pf).join("Windows Kits").join("10").join("bin");
        let Ok(entries) = std::fs::read_dir(&root) else { continue };
        let mut versions: Vec<PathBuf> = entries
            .filter_map(|e| e.ok().map(|e| e.path()))
            .filter(|p| p.file_name().and_then(|n| n.to_str()).is_some_and(|n| n.starts_with("10.")))
            .collect();
        versions.sort();
        for v in versions.into_iter().rev() {
            let rc = v.join(arch).join("rc.exe");
            if rc.exists() {
                return Some(rc);
            }
        }
    }
    None
}

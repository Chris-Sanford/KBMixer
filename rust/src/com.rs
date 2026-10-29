//! Minimal Core Audio (WASAPI) COM bindings: vtables in declaration order, plus an owning smart pointer.
#![allow(non_snake_case, dead_code)]

use crate::win::*;
use core::ffi::c_void;
use core::ptr::null_mut;
use std::sync::atomic::{AtomicU32, Ordering};

pub const S_OK: HRESULT = 0;
pub const S_FALSE: HRESULT = 1;
pub const E_NOINTERFACE: HRESULT = 0x80004002u32 as i32;
pub const E_POINTER: HRESULT = 0x80004003u32 as i32;

pub const CLSID_MMDEVICE_ENUMERATOR: GUID = GUID::from_u128(0xBCDE0395_E52F_467C_8E3D_C4579291692E);
pub const IID_IUNKNOWN: GUID = GUID::from_u128(0x00000000_0000_0000_C000_000000000046);
pub const IID_IMMDEVICE_ENUMERATOR: GUID = GUID::from_u128(0xA95664D2_9614_4F35_A746_DE8DB63617E6);
pub const IID_IAUDIO_ENDPOINT_VOLUME: GUID = GUID::from_u128(0x5CDF2C82_841E_4546_9722_0CF74078229A);
pub const IID_IAUDIO_SESSION_MANAGER2: GUID = GUID::from_u128(0x77AA99A0_1BD6_484F_8BC7_2C654C9A9B6F);
pub const IID_IAUDIO_SESSION_CONTROL2: GUID = GUID::from_u128(0xBFB7FF88_7239_4FC9_8FA2_07C950BE9C6D);
pub const IID_ISIMPLE_AUDIO_VOLUME: GUID = GUID::from_u128(0x87CE5498_68D6_44E5_9215_6DA47EF883D8);
pub const IID_IAUDIO_SESSION_NOTIFICATION: GUID = GUID::from_u128(0x641DD20B_4D41_49CC_ABA3_174B9477BB08);

pub const E_RENDER: u32 = 0;
pub const E_MULTIMEDIA: u32 = 1;
pub const DEVICE_STATE_ACTIVE: u32 = 0x1;
pub const STGM_READ: u32 = 0;

#[repr(C)]
pub struct PROPERTYKEY {
    pub fmtid: GUID,
    pub pid: u32,
}

/// Endpoint name as shown in Sound settings (user-renamable), e.g. "Speakers (USB DAC)".
pub const PKEY_DEVICE_FRIENDLY_NAME: PROPERTYKEY =
    PROPERTYKEY { fmtid: GUID::from_u128(0xA45C254E_DF1C_4EFD_8020_67D146A850E0), pid: 14 };
/// Adapter / interface name, e.g. "HyperX Cloud Alpha S Game" (NAudio's DeviceFriendlyName).
pub const PKEY_DEVICE_INTERFACE_FRIENDLY_NAME: PROPERTYKEY =
    PROPERTYKEY { fmtid: GUID::from_u128(0x026E516E_B814_414B_83CD_856D6FEF4822), pid: 2 };

/// PROPVARIANT is 24 bytes on 64-bit; we only read VT_LPWSTR.
#[repr(C)]
pub struct PROPVARIANT {
    pub vt: u16,
    pub r1: u16,
    pub r2: u16,
    pub r3: u16,
    pub data: [usize; 2],
}
const VT_LPWSTR: u16 = 31;

type This = *mut c_void;

#[repr(C)]
pub struct IUnknownVtbl {
    pub QueryInterface: unsafe extern "system" fn(This, *const GUID, *mut *mut c_void) -> HRESULT,
    pub AddRef: unsafe extern "system" fn(This) -> u32,
    pub Release: unsafe extern "system" fn(This) -> u32,
}

#[repr(C)]
pub struct IMMDeviceEnumeratorVtbl {
    pub base: IUnknownVtbl,
    pub EnumAudioEndpoints: unsafe extern "system" fn(This, u32, u32, *mut *mut c_void) -> HRESULT,
    pub GetDefaultAudioEndpoint: unsafe extern "system" fn(This, u32, u32, *mut *mut c_void) -> HRESULT,
    pub GetDevice: unsafe extern "system" fn(This, PCWSTR, *mut *mut c_void) -> HRESULT,
    pub RegisterEndpointNotificationCallback: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
    pub UnregisterEndpointNotificationCallback: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
}

#[repr(C)]
pub struct IMMDeviceCollectionVtbl {
    pub base: IUnknownVtbl,
    pub GetCount: unsafe extern "system" fn(This, *mut u32) -> HRESULT,
    pub Item: unsafe extern "system" fn(This, u32, *mut *mut c_void) -> HRESULT,
}

#[repr(C)]
pub struct IMMDeviceVtbl {
    pub base: IUnknownVtbl,
    pub Activate: unsafe extern "system" fn(This, *const GUID, u32, *mut c_void, *mut *mut c_void) -> HRESULT,
    pub OpenPropertyStore: unsafe extern "system" fn(This, u32, *mut *mut c_void) -> HRESULT,
    pub GetId: unsafe extern "system" fn(This, *mut PWSTR) -> HRESULT,
    pub GetState: unsafe extern "system" fn(This, *mut u32) -> HRESULT,
}

#[repr(C)]
pub struct IPropertyStoreVtbl {
    pub base: IUnknownVtbl,
    pub GetCount: unsafe extern "system" fn(This, *mut u32) -> HRESULT,
    pub GetAt: unsafe extern "system" fn(This, u32, *mut PROPERTYKEY) -> HRESULT,
    pub GetValue: unsafe extern "system" fn(This, *const PROPERTYKEY, *mut PROPVARIANT) -> HRESULT,
    pub SetValue: unsafe extern "system" fn(This, *const PROPERTYKEY, *const PROPVARIANT) -> HRESULT,
    pub Commit: unsafe extern "system" fn(This) -> HRESULT,
}

#[repr(C)]
pub struct IAudioEndpointVolumeVtbl {
    pub base: IUnknownVtbl,
    pub RegisterControlChangeNotify: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
    pub UnregisterControlChangeNotify: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
    pub GetChannelCount: unsafe extern "system" fn(This, *mut u32) -> HRESULT,
    pub SetMasterVolumeLevel: unsafe extern "system" fn(This, f32, *const GUID) -> HRESULT,
    pub SetMasterVolumeLevelScalar: unsafe extern "system" fn(This, f32, *const GUID) -> HRESULT,
    pub GetMasterVolumeLevel: unsafe extern "system" fn(This, *mut f32) -> HRESULT,
    pub GetMasterVolumeLevelScalar: unsafe extern "system" fn(This, *mut f32) -> HRESULT,
    pub SetChannelVolumeLevel: unsafe extern "system" fn(This, u32, f32, *const GUID) -> HRESULT,
    pub SetChannelVolumeLevelScalar: unsafe extern "system" fn(This, u32, f32, *const GUID) -> HRESULT,
    pub GetChannelVolumeLevel: unsafe extern "system" fn(This, u32, *mut f32) -> HRESULT,
    pub GetChannelVolumeLevelScalar: unsafe extern "system" fn(This, u32, *mut f32) -> HRESULT,
    pub SetMute: unsafe extern "system" fn(This, BOOL, *const GUID) -> HRESULT,
    pub GetMute: unsafe extern "system" fn(This, *mut BOOL) -> HRESULT,
}

#[repr(C)]
pub struct IAudioSessionManager2Vtbl {
    pub base: IUnknownVtbl,
    pub GetAudioSessionControl: unsafe extern "system" fn(This, *const GUID, u32, *mut *mut c_void) -> HRESULT,
    pub GetSimpleAudioVolume: unsafe extern "system" fn(This, *const GUID, u32, *mut *mut c_void) -> HRESULT,
    pub GetSessionEnumerator: unsafe extern "system" fn(This, *mut *mut c_void) -> HRESULT,
    pub RegisterSessionNotification: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
    pub UnregisterSessionNotification: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
}

#[repr(C)]
pub struct IAudioSessionEnumeratorVtbl {
    pub base: IUnknownVtbl,
    pub GetCount: unsafe extern "system" fn(This, *mut i32) -> HRESULT,
    pub GetSession: unsafe extern "system" fn(This, i32, *mut *mut c_void) -> HRESULT,
}

#[repr(C)]
pub struct IAudioSessionControl2Vtbl {
    pub base: IUnknownVtbl,
    pub GetState: unsafe extern "system" fn(This, *mut u32) -> HRESULT,
    pub GetDisplayName: unsafe extern "system" fn(This, *mut PWSTR) -> HRESULT,
    pub SetDisplayName: unsafe extern "system" fn(This, PCWSTR, *const GUID) -> HRESULT,
    pub GetIconPath: unsafe extern "system" fn(This, *mut PWSTR) -> HRESULT,
    pub SetIconPath: unsafe extern "system" fn(This, PCWSTR, *const GUID) -> HRESULT,
    pub GetGroupingParam: unsafe extern "system" fn(This, *mut GUID) -> HRESULT,
    pub SetGroupingParam: unsafe extern "system" fn(This, *const GUID, *const GUID) -> HRESULT,
    pub RegisterAudioSessionNotification: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
    pub UnregisterAudioSessionNotification: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
    // IAudioSessionControl2
    pub GetSessionIdentifier: unsafe extern "system" fn(This, *mut PWSTR) -> HRESULT,
    pub GetSessionInstanceIdentifier: unsafe extern "system" fn(This, *mut PWSTR) -> HRESULT,
    pub GetProcessId: unsafe extern "system" fn(This, *mut u32) -> HRESULT,
    pub IsSystemSoundsSession: unsafe extern "system" fn(This) -> HRESULT,
    pub SetDuckingPreference: unsafe extern "system" fn(This, BOOL) -> HRESULT,
}

#[repr(C)]
pub struct ISimpleAudioVolumeVtbl {
    pub base: IUnknownVtbl,
    pub SetMasterVolume: unsafe extern "system" fn(This, f32, *const GUID) -> HRESULT,
    pub GetMasterVolume: unsafe extern "system" fn(This, *mut f32) -> HRESULT,
    pub SetMute: unsafe extern "system" fn(This, BOOL, *const GUID) -> HRESULT,
    pub GetMute: unsafe extern "system" fn(This, *mut BOOL) -> HRESULT,
}

/// Owning COM pointer. `V` is the interface's vtable type.
pub struct Com<V> {
    ptr: *mut *const V,
}

// Every object we hold is created and used on the MTA audio worker; MTA objects may move between MTA threads.
unsafe impl<V> Send for Com<V> {}

impl<V> Com<V> {
    /// Takes ownership of one reference.
    ///
    /// # Safety
    /// `p` must be null or a valid interface pointer whose vtable layout is `V`.
    pub unsafe fn from_raw(p: *mut c_void) -> Option<Self> {
        if p.is_null() {
            None
        } else {
            Some(Com { ptr: p as *mut *const V })
        }
    }

    #[inline]
    pub fn raw(&self) -> This {
        self.ptr as This
    }

    #[inline]
    pub fn vt(&self) -> &V {
        unsafe { &**self.ptr }
    }

    #[inline]
    fn unk(&self) -> &IUnknownVtbl {
        unsafe { &*(*self.ptr as *const IUnknownVtbl) }
    }

    pub fn query<U>(&self, iid: &GUID) -> Option<Com<U>> {
        let mut out = null_mut();
        let hr = unsafe { (self.unk().QueryInterface)(self.raw(), iid, &mut out) };
        if hr < 0 {
            return None;
        }
        unsafe { Com::from_raw(out) }
    }
}

impl<V> Clone for Com<V> {
    fn clone(&self) -> Self {
        unsafe { (self.unk().AddRef)(self.raw()) };
        Com { ptr: self.ptr }
    }
}

impl<V> Drop for Com<V> {
    fn drop(&mut self) {
        unsafe { (self.unk().Release)(self.raw()) };
    }
}

pub type DeviceEnumerator = Com<IMMDeviceEnumeratorVtbl>;
pub type Device = Com<IMMDeviceVtbl>;
pub type EndpointVolume = Com<IAudioEndpointVolumeVtbl>;
pub type SessionManager = Com<IAudioSessionManager2Vtbl>;
pub type SessionControl = Com<IAudioSessionControl2Vtbl>;
pub type SimpleVolume = Com<ISimpleAudioVolumeVtbl>;

fn take_co_string(p: PWSTR) -> String {
    let s = unsafe { from_wide_ptr(p) };
    if !p.is_null() {
        unsafe { CoTaskMemFree(p as *mut c_void) };
    }
    s
}

pub type HResult<T> = Result<T, HRESULT>;

#[inline]
fn check(hr: HRESULT) -> HResult<()> {
    if hr < 0 { Err(hr) } else { Ok(()) }
}

impl DeviceEnumerator {
    pub fn create() -> HResult<Self> {
        let mut p = null_mut();
        check(unsafe {
            CoCreateInstance(&CLSID_MMDEVICE_ENUMERATOR, null_mut(), CLSCTX_ALL, &IID_IMMDEVICE_ENUMERATOR, &mut p)
        })?;
        unsafe { Com::from_raw(p) }.ok_or(E_POINTER)
    }

    pub fn active_render_devices(&self) -> HResult<Vec<Device>> {
        let mut coll = null_mut();
        check(unsafe { (self.vt().EnumAudioEndpoints)(self.raw(), E_RENDER, DEVICE_STATE_ACTIVE, &mut coll) })?;
        let coll: Com<IMMDeviceCollectionVtbl> = unsafe { Com::from_raw(coll) }.ok_or(E_POINTER)?;
        let mut n = 0u32;
        check(unsafe { (coll.vt().GetCount)(coll.raw(), &mut n) })?;
        let mut out = Vec::with_capacity(n as usize);
        for i in 0..n {
            let mut d = null_mut();
            if unsafe { (coll.vt().Item)(coll.raw(), i, &mut d) } >= 0 {
                if let Some(d) = unsafe { Com::from_raw(d) } {
                    out.push(d);
                }
            }
        }
        Ok(out)
    }

    pub fn default_render_device(&self) -> HResult<Device> {
        let mut d = null_mut();
        check(unsafe { (self.vt().GetDefaultAudioEndpoint)(self.raw(), E_RENDER, E_MULTIMEDIA, &mut d) })?;
        unsafe { Com::from_raw(d) }.ok_or(E_POINTER)
    }
}

impl Device {
    pub fn id(&self) -> HResult<String> {
        let mut p = null_mut();
        check(unsafe { (self.vt().GetId)(self.raw(), &mut p) })?;
        Ok(take_co_string(p))
    }

    fn activate<U>(&self, iid: &GUID) -> HResult<Com<U>> {
        let mut p = null_mut();
        check(unsafe { (self.vt().Activate)(self.raw(), iid, CLSCTX_ALL, null_mut(), &mut p) })?;
        unsafe { Com::from_raw(p) }.ok_or(E_POINTER)
    }

    pub fn endpoint_volume(&self) -> HResult<EndpointVolume> {
        self.activate(&IID_IAUDIO_ENDPOINT_VOLUME)
    }

    pub fn session_manager(&self) -> HResult<SessionManager> {
        self.activate(&IID_IAUDIO_SESSION_MANAGER2)
    }

    /// (endpoint friendly name, adapter description). Direct property reads: cheap, unlike NAudio's store scan.
    pub fn names(&self) -> (String, Option<String>) {
        let mut store = null_mut();
        if unsafe { (self.vt().OpenPropertyStore)(self.raw(), STGM_READ, &mut store) } < 0 {
            return (String::new(), None);
        }
        let Some(store): Option<Com<IPropertyStoreVtbl>> = (unsafe { Com::from_raw(store) }) else {
            return (String::new(), None);
        };
        let read = |key: &PROPERTYKEY| -> Option<String> {
            let mut pv = PROPVARIANT { vt: 0, r1: 0, r2: 0, r3: 0, data: [0; 2] };
            if unsafe { (store.vt().GetValue)(store.raw(), key, &mut pv) } < 0 {
                return None;
            }
            let s = if pv.vt == VT_LPWSTR { Some(unsafe { from_wide_ptr(pv.data[0] as *const u16) }) } else { None };
            unsafe { PropVariantClear(&mut pv as *mut _ as *mut c_void) };
            s
        };
        let friendly = read(&PKEY_DEVICE_FRIENDLY_NAME).unwrap_or_default();
        let desc = read(&PKEY_DEVICE_INTERFACE_FRIENDLY_NAME)
            .map(|s| s.trim().to_string())
            .filter(|s| !s.is_empty());
        (friendly, desc)
    }
}

impl EndpointVolume {
    pub fn scalar(&self) -> HResult<f32> {
        let mut v = 0f32;
        check(unsafe { (self.vt().GetMasterVolumeLevelScalar)(self.raw(), &mut v) })?;
        Ok(v)
    }
    pub fn set_scalar(&self, v: f32) -> HResult<()> {
        check(unsafe { (self.vt().SetMasterVolumeLevelScalar)(self.raw(), v.clamp(0.0, 1.0), core::ptr::null()) })
    }
    pub fn mute(&self) -> HResult<bool> {
        let mut m = 0;
        check(unsafe { (self.vt().GetMute)(self.raw(), &mut m) })?;
        Ok(m != 0)
    }
    pub fn set_mute(&self, m: bool) -> HResult<()> {
        check(unsafe { (self.vt().SetMute)(self.raw(), m as BOOL, core::ptr::null()) })
    }
}

impl SessionManager {
    pub fn sessions(&self) -> HResult<Vec<SessionControl>> {
        let mut e = null_mut();
        check(unsafe { (self.vt().GetSessionEnumerator)(self.raw(), &mut e) })?;
        let e: Com<IAudioSessionEnumeratorVtbl> = unsafe { Com::from_raw(e) }.ok_or(E_POINTER)?;
        let mut n = 0i32;
        check(unsafe { (e.vt().GetCount)(e.raw(), &mut n) })?;
        let mut out = Vec::with_capacity(n.max(0) as usize);
        for i in 0..n {
            let mut s = null_mut();
            if unsafe { (e.vt().GetSession)(e.raw(), i, &mut s) } < 0 {
                continue;
            }
            // GetSession returns IAudioSessionControl; take the v2 interface and drop the v1 reference.
            let Some(v1): Option<Com<IUnknownVtbl>> = (unsafe { Com::from_raw(s) }) else { continue };
            if let Some(v2) = v1.query::<IAudioSessionControl2Vtbl>(&IID_IAUDIO_SESSION_CONTROL2) {
                out.push(v2);
            }
        }
        Ok(out)
    }

    pub fn register(&self, n: &SessionNotifier) -> HResult<()> {
        check(unsafe { (self.vt().RegisterSessionNotification)(self.raw(), n.raw()) })
    }

    pub fn unregister(&self, n: &SessionNotifier) {
        unsafe { (self.vt().UnregisterSessionNotification)(self.raw(), n.raw()) };
    }
}

impl SessionControl {
    pub fn process_id(&self) -> u32 {
        let mut pid = 0;
        unsafe { (self.vt().GetProcessId)(self.raw(), &mut pid) };
        pid
    }
    pub fn is_system_sounds(&self) -> bool {
        unsafe { (self.vt().IsSystemSoundsSession)(self.raw()) == S_OK }
    }
    pub fn instance_id(&self) -> String {
        let mut p = null_mut();
        if unsafe { (self.vt().GetSessionInstanceIdentifier)(self.raw(), &mut p) } < 0 {
            return String::new();
        }
        take_co_string(p)
    }
    pub fn display_name(&self) -> String {
        let mut p = null_mut();
        if unsafe { (self.vt().GetDisplayName)(self.raw(), &mut p) } < 0 {
            return String::new();
        }
        take_co_string(p)
    }
    pub fn simple_volume(&self) -> Option<SimpleVolume> {
        self.query(&IID_ISIMPLE_AUDIO_VOLUME)
    }
}

impl SimpleVolume {
    pub fn volume(&self) -> HResult<f32> {
        let mut v = 0f32;
        check(unsafe { (self.vt().GetMasterVolume)(self.raw(), &mut v) })?;
        Ok(v)
    }
    pub fn set_volume(&self, v: f32) -> HResult<()> {
        check(unsafe { (self.vt().SetMasterVolume)(self.raw(), v.clamp(0.0, 1.0), core::ptr::null()) })
    }
    pub fn mute(&self) -> HResult<bool> {
        let mut m = 0;
        check(unsafe { (self.vt().GetMute)(self.raw(), &mut m) })?;
        Ok(m != 0)
    }
    pub fn set_mute(&self, m: bool) -> HResult<()> {
        check(unsafe { (self.vt().SetMute)(self.raw(), m as BOOL, core::ptr::null()) })
    }
}

// ───── IAudioSessionNotification implemented in Rust ─────

#[repr(C)]
struct NotifierVtbl {
    base: IUnknownVtbl,
    OnSessionCreated: unsafe extern "system" fn(This, *mut c_void) -> HRESULT,
}

#[repr(C)]
struct NotifierObj {
    vtbl: *const NotifierVtbl,
    refs: AtomicU32,
    callback: Box<dyn Fn() + Send + Sync>,
}

static NOTIFIER_VTBL: NotifierVtbl = NotifierVtbl {
    base: IUnknownVtbl { QueryInterface: notifier_qi, AddRef: notifier_addref, Release: notifier_release },
    OnSessionCreated: notifier_created,
};

unsafe extern "system" fn notifier_qi(this: This, iid: *const GUID, out: *mut *mut c_void) -> HRESULT {
    if out.is_null() {
        return E_POINTER;
    }
    if *iid == IID_IUNKNOWN || *iid == IID_IAUDIO_SESSION_NOTIFICATION {
        notifier_addref(this);
        *out = this;
        S_OK
    } else {
        *out = null_mut();
        E_NOINTERFACE
    }
}

unsafe extern "system" fn notifier_addref(this: This) -> u32 {
    (*(this as *const NotifierObj)).refs.fetch_add(1, Ordering::Relaxed) + 1
}

unsafe extern "system" fn notifier_release(this: This) -> u32 {
    let left = (*(this as *const NotifierObj)).refs.fetch_sub(1, Ordering::AcqRel) - 1;
    if left == 0 {
        drop(Box::from_raw(this as *mut NotifierObj));
    }
    left
}

unsafe extern "system" fn notifier_created(this: This, _session: *mut c_void) -> HRESULT {
    // The new session is owned by the caller; we only need to know that something appeared.
    ((*(this as *const NotifierObj)).callback)();
    S_OK
}

/// A COM object that invokes `callback` from the audio service's notification thread when a session is created.
pub struct SessionNotifier {
    ptr: *mut NotifierObj,
}

unsafe impl Send for SessionNotifier {}

impl SessionNotifier {
    pub fn new(callback: Box<dyn Fn() + Send + Sync>) -> Self {
        let obj = Box::new(NotifierObj { vtbl: &NOTIFIER_VTBL, refs: AtomicU32::new(1), callback });
        SessionNotifier { ptr: Box::into_raw(obj) }
    }
    fn raw(&self) -> This {
        self.ptr as This
    }
}

impl Drop for SessionNotifier {
    fn drop(&mut self) {
        unsafe { notifier_release(self.raw()) };
    }
}

pub struct ComInit;

impl ComInit {
    pub fn mta() -> ComInit {
        unsafe { CoInitializeEx(core::ptr::null(), COINIT_MULTITHREADED) };
        ComInit
    }
    pub fn sta() -> ComInit {
        unsafe { CoInitializeEx(core::ptr::null(), COINIT_APARTMENTTHREADED) };
        ComInit
    }
}

impl Drop for ComInit {
    fn drop(&mut self) {
        unsafe { CoUninitialize() };
    }
}

pub fn new_guid() -> GUID {
    let mut g = GUID::default();
    unsafe { CoCreateGuid(&mut g) };
    g
}

/// "d" format, lowercase: 3e0d19f1-6ba0-48c7-b886-f2f6ce0a4802 (same as .NET Guid.ToString()).
pub fn guid_to_string(g: &GUID) -> String {
    let d = &g.data4;
    format!(
        "{:08x}-{:04x}-{:04x}-{:02x}{:02x}-{:02x}{:02x}{:02x}{:02x}{:02x}{:02x}",
        g.data1, g.data2, g.data3, d[0], d[1], d[2], d[3], d[4], d[5], d[6], d[7]
    )
}

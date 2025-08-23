use std::ffi::{CStr, c_char};

use chrono::{Datelike, Days, Local, NaiveDateTime, NaiveTime, Weekday};
use opening_hours::OpeningHours;

#[no_mangle]
pub unsafe extern "C" fn is_open_at_lunch(pattern: *const c_char) -> bool {
    let pattern_str = unsafe { CStr::from_ptr(pattern) };
    pattern_str
        .to_str()
        .map(is_open_at_lunch_internal)
        .unwrap_or(false)
}

fn is_open_at_lunch_internal(pattern: &str) -> bool {
    pattern
        .parse::<OpeningHours>()
        .map(|p| p.is_open(weekday_at_12()))
        .unwrap_or(false)
}

fn weekday_at_12() -> NaiveDateTime {
    let at_12 = Local::now()
        .with_time(NaiveTime::from_hms_opt(12, 00, 01).unwrap())
        .unwrap()
        .naive_local();
    match at_12.weekday() {
        Weekday::Sat => at_12.checked_add_days(Days::new(2)).unwrap(),
        Weekday::Sun => at_12.checked_add_days(Days::new(1)).unwrap(),
        _ => at_12,
    }
}

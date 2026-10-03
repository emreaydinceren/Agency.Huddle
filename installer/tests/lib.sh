# shellcheck shell=sh
# Tiny assertion harness for installer/tests/*_test.sh. Source it, call t_run for each test
# function, finish with t_summary. POSIX sh only (macOS bash 3.2, dash).
#
#   t_assert_eq <expected> <actual> <label>
#   t_assert_contains <haystack> <needle> <label>
#   t_assert_not_contains <haystack> <needle> <label>
#   t_assert_file <path> <label>          the path exists as a regular file
#   t_assert_no_file <path> <label>       the path does not exist at all
#   t_run <function-name>                 runs the function in the current shell
#   t_summary                             prints totals; status 1 when anything failed
#
# T_SHELL is the shell tests use when they launch install.sh as a child process.

T_PASS=0
T_FAIL=0
T_CURRENT=""
T_SHELL="${T_SHELL:-sh}"

t_pass() {
    T_PASS=$((T_PASS + 1))
}

t_fail() {
    T_FAIL=$((T_FAIL + 1))
    printf 'FAIL [%s] %s\n' "$T_CURRENT" "$1"
}

t_assert_eq() {
    if [ "$1" = "$2" ]; then
        t_pass
    else
        t_fail "$3: expected '$1' but got '$2'"
    fi
}

t_assert_contains() {
    case $1 in
        *"$2"*) t_pass ;;
        *) t_fail "$3: expected '$2' inside '$1'" ;;
    esac
}

t_assert_not_contains() {
    case $1 in
        *"$2"*) t_fail "$3: did not expect '$2' inside '$1'" ;;
        *) t_pass ;;
    esac
}

t_assert_file() {
    if [ -f "$1" ]; then
        t_pass
    else
        t_fail "$2: expected file '$1' to exist"
    fi
}

t_assert_no_file() {
    if [ -e "$1" ]; then
        t_fail "$2: expected '$1' not to exist"
    else
        t_pass
    fi
}

t_run() {
    T_CURRENT=$1
    printf 'RUN  %s\n' "$1"
    "$1" || t_fail "test function returned non-zero"
}

t_summary() {
    printf '%s passed, %s failed\n' "$T_PASS" "$T_FAIL"
    [ "$T_FAIL" -eq 0 ]
}

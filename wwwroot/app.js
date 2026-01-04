window.vacationTracker = window.vacationTracker || {};

window.vacationTracker.setCookie = (name, value, days) => {
    const maxAge = days ? `; max-age=${days * 24 * 60 * 60}` : "";
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}${maxAge}; path=/; samesite=lax`;
};

window.vacationTracker.clearCookie = (name) => {
    document.cookie = `${encodeURIComponent(name)}=; max-age=0; path=/; samesite=lax`;
};

window.vacationTracker.initNavDropdowns = () => {
    if (window.vacationTracker.navDropdownsBound) {
        return;
    }

    window.vacationTracker.navDropdownsBound = true;

    document.addEventListener("click", (event) => {
        const openDropdowns = document.querySelectorAll(".app-nav__dropdown[open]");
        openDropdowns.forEach((dropdown) => {
            if (!dropdown.contains(event.target)) {
                dropdown.removeAttribute("open");
            }
        });
    });
};

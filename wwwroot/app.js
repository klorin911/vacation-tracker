window.vacationTracker = window.vacationTracker || {};

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

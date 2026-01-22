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

    const navToggle = document.getElementById("nav-toggle");
    const navLinks = document.querySelector(".app-nav__links");
    // Close desktop dropdown when clicking outside
    document.addEventListener("click", (event) => {
        const openDropdowns = document.querySelectorAll(".app-nav__dropdown[open]");
        openDropdowns.forEach((dropdown) => {
            if (!dropdown.contains(event.target)) {
                dropdown.removeAttribute("open");
            }
        });

        // Close mobile nav when clicking outside
        if (navToggle && navToggle.checked) {
            const isClickInsideNav = navLinks && navLinks.contains(event.target);
            const isClickOnToggle = event.target.closest(".app-nav__toggle-btn") || event.target.id === "nav-toggle";
            
            if (!isClickInsideNav && !isClickOnToggle) {
                navToggle.checked = false;
            }
        }
    });

    // Close mobile nav when clicking a link
    if (navLinks) {
        navLinks.addEventListener("click", (event) => {
            const clickedLink = event.target.closest("a.app-nav__link, a.app-nav__menu-item, a.app-nav__mobile-logout");
            if (clickedLink && navToggle) {
                navToggle.checked = false;
            }
        });
    }

    // Close mobile nav on scroll
    let scrollTimeout;
    window.addEventListener("scroll", () => {
        if (navToggle && navToggle.checked) {
            clearTimeout(scrollTimeout);
            scrollTimeout = setTimeout(() => {
                navToggle.checked = false;
            }, 50);
        }
    }, { passive: true });
};

window.vacationTracker.getSystemTheme = () =>
    window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";

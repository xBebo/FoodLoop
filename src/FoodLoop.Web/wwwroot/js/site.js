// Presentation-only layout behaviour. Everything here is optional: without it the page and navigation still work.
(function () {
    'use strict';

    var header = document.querySelector('.app-header');
    if (!header) return;

    // Header scroll state: adds a soft shadow/translucency once the page has moved.
    var onScroll = function () { header.classList.toggle('is-scrolled', window.scrollY > 8); };
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });

    // Mobile menu: lock page scroll while Bootstrap's collapse is open, close on Escape or outside tap.
    var nav = document.getElementById('foodloopNav');
    var toggler = header.querySelector('.navbar-toggler');
    var collapse = window.bootstrap && window.bootstrap.Collapse;
    if (!nav || !toggler || !collapse) return;

    var body = document.body;
    var desktop = window.matchMedia('(min-width: 992px)');
    var lock = function (on) { body.classList.toggle('fl-nav-open', on && !desktop.matches); };
    var hide = function () { if (nav.classList.contains('show')) collapse.getOrCreateInstance(nav, { toggle: false }).hide(); };

    nav.addEventListener('show.bs.collapse', function () { lock(true); });
    nav.addEventListener('hide.bs.collapse', function () { lock(false); });
    var onViewport = function () { lock(nav.classList.contains('show')); };
    if (desktop.addEventListener) desktop.addEventListener('change', onViewport);

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && body.classList.contains('fl-nav-open')) { hide(); toggler.focus(); }
    });
    document.addEventListener('click', function (e) {
        if (body.classList.contains('fl-nav-open') && !header.contains(e.target)) hide();
    });
})();

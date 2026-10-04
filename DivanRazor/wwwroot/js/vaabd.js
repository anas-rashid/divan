// Create tooltip elements
var $meaning = $('<a>لغت</a>').css({
    padding: '10px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
});

var $abjad = $('<a>ابجد</a>').css({
    padding: '10px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
});

var $search = $('<a>🔍</a>').css({
    padding: '10px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
});

var $quran = $('<a>قرآن</a>').css({
    padding: '10px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
});

var $vazn = $('<a>بحر</a>').css({
    padding: '10px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
});

var $google = $('<a>گوگل</a>').css({
    padding: '10px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
});

var $close = $('<a href="#" id="vaabx">غیر فعال ہو</a>').css({
    cursor: "pointer",
    padding: "10px",
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    textAlign: 'center',
    borderTop: '1px solid #c9a050', // Visual separator
});

// Tooltip container styling
var $tooltip = $('<div>').addClass('tooltip').css({
    transform: 'scale(0)',
    transformOrigin: 'top',
    position: 'absolute',
    borderRadius: '10px',
    display: 'flex',
    flexDirection: 'column', // Vertical layout
    background: 'rgba(14,17,17,0.9)',
    transition: 'transform 0.2s ease-out',
    zIndex: '2000',
    maxWidth: '90%', // Responsive width for smaller screens
    wordWrap: 'break-word',
    padding: '5px',
});

// Append links and close button to tooltip
$tooltip.append($meaning, $quran, $search, $google, $vazn, $close); // divan: abjad (divan service) dropped

// Append tooltip to body
$(document.body).append($tooltip);

// Attach click event to the close button
$close.on("click", function (event) {
    event.preventDefault();
    alert('مینو عارضی طور پر بند ہو گیا.\r\nدوبارہ فعال کرنے کے لیے صفحہ دوبارہ لوڈ کریں.');
    $tooltip.css({ transform: 'scale(0)' });
    document.removeEventListener('selectionchange', vaabSelectionChanged);
});

// Handle selection changes
var prevtext = '';
function vaabSelectionChanged() {
    var sel = window.getSelection() || document.getSelection();
    var text = sel.toString().trim();

    if (!text) {
        $tooltip.css({ transform: 'scale(0)' });
        prevtext = '';
        return;
    }

    // Normalize text (same logic as before)
    text = text.replaceAll("‌", " ")
        .replaceAll("ّ", "")
        .replaceAll("َ", "")
        .replaceAll("ِ", "")
        .replaceAll("ُ", "")
        .replaceAll("ً", "")
        .replaceAll("ٍ", "")
        .replaceAll("ٌ", "")
        .replaceAll(".", "")
        .replaceAll("،", "")
        .replaceAll("!", "")
        .replaceAll("؟", "")
        .replaceAll("ٔ", "")
        .replaceAll(":", "")
        .replaceAll("ئ", "ی")
        .replaceAll("؛", "")
        .replaceAll(";", "")
        .replaceAll("*", "")
        .replaceAll(")", "")
        .replaceAll("(", "")
        .replaceAll("[", "")
        .replaceAll("]", "")
        .replaceAll("\"", "")
        .replaceAll("'", "")
        .replaceAll("«", "")
        .replaceAll("»", "")
        .replaceAll("ْ", "");

    // Update tooltip links
    $meaning.attr({
        href: 'https://ur.wiktionary.org/w/index.php?search=' + encodeURI(text),
        title: 'ویکی لغت میں معنی',
        target: '_blank',
    });
    $search.attr({
        href: text.indexOf(' ') == -1 ? '/search?s=' + encodeURI(text) : '/search?s="' + encodeURI(text) + '"',
        title: 'دیوان میں عبارت تلاش کریں',
        target: '_blank',
    });
    $quran.attr({
        href: 'https://tanzil.ir/#search/quran/' + encodeURI(text),
        title: 'قرآن میں عبارت تلاش کریں',
        target: '_blank',
    });
    $vazn.attr({
        href: 'http://sorud.info/?Text=' + encodeURI(text),
        title: 'عبارت کی بحر معلوم کریں',
        target: '_blank',
    });
    $google.attr({
        href: 'https://www.google.com/search?q=' + encodeURI(text),
        title: 'گوگل میں تلاش',
        target: '_blank',
    });

    // Position tooltip near the selection
    var rect = sel.getRangeAt(0).getBoundingClientRect();
    var tooltipWidth = $tooltip.outerWidth();

    $tooltip.css({
        transform: 'scale(1)',
        top: rect.bottom + window.scrollY + 10, // Place below the selection
        left: rect.left + (rect.width / 2) - (tooltipWidth / 2), // Center tooltip horizontally
    });

    prevtext = text;
}

// Attach selection change listener
document.addEventListener('selectionchange', vaabSelectionChanged);

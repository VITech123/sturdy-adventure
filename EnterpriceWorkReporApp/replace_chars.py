import io
import sys

def main():
    try:
        with io.open('d:/EnterpriceWorkReporApp/Views/Pages/DashboardPage.xaml', 'r', encoding='utf-8') as f:
            content = f.read()

        replacements = {
            'âš™': '&#x2699;',
            'ðŸ“ ': '&#x1F4CB;',
            'ðŸ“„': '&#x1F4C4;',
            'ðŸ”¤': '&#x1F524;',
            'â‚¹': '&#x20B9;',
            'â °': '&#x23F0;',
            'â–¶': '&#x25B6;',
            'â ¹': '&#x23F9;',
            'ðŸ‘¥': '&#x1F465;',
            'ðŸ“ˆ': '&#x1F4C8;',
            'ðŸ“Š': '&#x1F4CA;',
            'ðŸ“…': '&#x1F4C5;',
            'â­ ': '&#x2B50;',
            'ðŸ’¬': '&#x1F4AC;'
        }

        for old, new in replacements.items():
            content = content.replace(old, new)

        with io.open('d:/EnterpriceWorkReporApp/Views/Pages/DashboardPage.xaml', 'w', encoding='utf-8') as f:
            f.write(content)
        print('Successfully replaced garbled characters.')
    except Exception as e:
        print(f"Error: {e}")

if __name__ == '__main__':
    main()

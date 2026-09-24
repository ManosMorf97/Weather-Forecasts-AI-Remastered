import smtplib
from email.message import EmailMessage

HOST = "smtp.gmail.com"
PORT = 587
USERNAME = "emmanouilmorfiadakis@gmail.com"  # Fill in your Gmail address - double check it, this is what caused the last failure
APP_PASSWORD = "txfw egae qzmi bdod"  # Fill in the app password generated for that exact address

if not USERNAME or not APP_PASSWORD:
    raise SystemExit("Set USERNAME and APP_PASSWORD before running.")

# Construct a test email message
msg = EmailMessage()
msg["Subject"] = "Test Email from SMTP Diagnostic"
msg["From"] = USERNAME
msg["To"] = USERNAME  # Sends a test email to yourself
msg.set_content("If you receive this message, your Gmail SMTP connection and authentication are working correctly!")

try:
    with smtplib.SMTP(HOST, PORT, timeout=15) as server:
        server.set_debuglevel(1)  # Prints raw SMTP conversation
        server.ehlo()
        server.starttls()
        server.ehlo()
        server.login(USERNAME, APP_PASSWORD)
        print("\nSUCCESS: Authenticated with Gmail.")

        # Send the message
        server.send_message(msg)
        print("SUCCESS: Test email dispatched successfully.")

except smtplib.SMTPAuthenticationError as exc:
    print(f"\nFAILED (auth rejected): {exc.smtp_code} {exc.smtp_error}")
except Exception as exc:
    print(f"\nFAILED (other error): {exc}")
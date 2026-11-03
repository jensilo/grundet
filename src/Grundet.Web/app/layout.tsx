import "./globals.css";

import type { Metadata } from "next";
import { DM_Serif_Display, Geist_Mono, Inter } from "next/font/google";

import { Toaster } from "@/components/ui/toast";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ApiConfigProvider } from "@/providers/api-config-provider";

const inter = Inter({ variable: "--font-inter", subsets: ["latin"], display: "swap" });
const dmSerifDisplay = DM_Serif_Display({
  variable: "--font-dm-serif",
  subsets: ["latin"],
  weight: "400",
  display: "swap",
});
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Grundet",
  description: "Versioned requirements, decisions, and work items for software teams.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      className={`${inter.variable} ${dmSerifDisplay.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="bg-background text-foreground min-h-full">
        <ApiConfigProvider>
          <TooltipProvider>{children}</TooltipProvider>
        </ApiConfigProvider>
        <Toaster />
      </body>
    </html>
  );
}

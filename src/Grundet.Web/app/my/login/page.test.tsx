import { render, screen } from "@testing-library/react";
import { redirect } from "next/navigation";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { getMe } from "@/lib/api/generated/sdk.gen";

import { LoginForm } from "./login-form";
import LoginPage from "./page";

let pendingInviteToken: string | undefined;

vi.mock("next/headers", () => ({
  headers: async () => ({
    get: (name: string) => (name === "host" ? "my.grundet.test:3000" : null),
  }),
  cookies: async () => ({
    get: (name: string) =>
      name === "grundet_pending_invite_token" && pendingInviteToken
        ? { value: pendingInviteToken }
        : undefined,
  }),
}));

vi.mock("@/lib/api/generated/sdk.gen", () => ({
  sendMagicLink: vi.fn(async () => ({ data: { message: "ok" } })),
  getMe: vi.fn(),
}));

vi.mock("@/lib/api/server", () => ({
  createServerClient: vi.fn(async () => ({})),
}));

vi.mock("next/navigation", () => ({
  // The real redirect throws to perform the control-flow jump — mirror that so we can assert it.
  redirect: vi.fn((url: string) => {
    throw new Error(`NEXT_REDIRECT:${url}`);
  }),
}));

const mockedGetMe = vi.mocked(getMe);
const mockedRedirect = vi.mocked(redirect);

describe("LoginForm", () => {
  it("links users without alpha access to the provided Join Alpha page", () => {
    render(<LoginForm joinAlphaUrl="http://grundet.test:3000/join-alpha" />);

    const link = screen.getByRole("link", { name: /Join the alpha/i });

    expect(screen.getByText(/Need alpha access first/i)).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "http://grundet.test:3000/join-alpha");
  });

  it("shows the invitation context instead of the alpha pitch for invitees", () => {
    render(<LoginForm joinAlphaUrl="http://grundet.test:3000/join-alpha" hasPendingInvite />);

    expect(screen.getByText("You have a team invitation")).toBeInTheDocument();
    expect(
      screen.getByText(/new accounts are approved automatically for invites/i)
    ).toBeInTheDocument();
    expect(screen.queryByText(/Need alpha access first/i)).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Join the alpha/i })).not.toBeInTheDocument();
  });
});

describe("LoginPage", () => {
  beforeEach(() => {
    // Unauthenticated visitor: LoginPage falls through to render the form.
    mockedGetMe.mockResolvedValue({
      data: undefined,
      error: { status: 401 },
    } as Awaited<ReturnType<typeof getMe>>);
  });

  it("passes the pending-invite state from the cookie to the form", async () => {
    pendingInviteToken = "abc123";

    const element = await LoginPage();

    expect(element.props.hasPendingInvite).toBe(true);
  });

  it("renders a plain login without the invite cookie", async () => {
    pendingInviteToken = undefined;

    const element = await LoginPage();

    expect(element.props.hasPendingInvite).toBe(false);
    expect(element.props.joinAlphaUrl).toBe("http://grundet.test:3000/join-alpha");
  });
});

describe("LoginPage – authenticated redirect", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("redirects an authenticated user to the dashboard", async () => {
    mockedGetMe.mockResolvedValue({
      data: { id: "u1", email: "a@example.com", displayName: "A" },
      error: undefined,
    } as Awaited<ReturnType<typeof getMe>>);

    await expect(LoginPage()).rejects.toThrow("NEXT_REDIRECT:/dashboard");
    expect(mockedRedirect).toHaveBeenCalledWith("/dashboard");
  });

  it("renders the login form for an unauthenticated visitor", async () => {
    mockedGetMe.mockResolvedValue({
      data: undefined,
      error: { status: 401 },
    } as Awaited<ReturnType<typeof getMe>>);

    render(await LoginPage());

    expect(mockedRedirect).not.toHaveBeenCalled();
    expect(screen.getByText(/Need alpha access first/i)).toBeInTheDocument();
  });
});

import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { SignUpViewModel } from "../viewModels/SignUpViewModel";

export const SignUp = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new SignUpViewModel();
    vm.initialize();
    return vm;
  }, []);

  return (
    <div className="signup-page">
      <form className="signup-form" onSubmit={viewModel.handleSubmit}>
        <h1>Sign Up</h1>

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          value={viewModel.email}
          onChange={(e) => viewModel.setEmail(e.target.value)}
          required
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          value={viewModel.password}
          onChange={(e) => viewModel.setPassword(e.target.value)}
          required
        />

        <label htmlFor="firstName">First Name</label>
        <input
          id="firstName"
          type="text"
          value={viewModel.firstName}
          onChange={(e) => viewModel.setFirstName(e.target.value)}
          required
        />

        <label htmlFor="lastName">Last Name</label>
        <input
          id="lastName"
          type="text"
          value={viewModel.lastName}
          onChange={(e) => viewModel.setLastName(e.target.value)}
          required
        />

        <button type="submit">Sign Up</button>
      </form>
    </div>
  );
});
